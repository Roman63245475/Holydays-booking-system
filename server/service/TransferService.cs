using be;
using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

// transfer (overførsel) of unused holiday to the next holiday period (see HolidayTransferRequest): the employee
// asks, the accountant approves (= the written agreement) or declines. the rules are in
// AccountStatusService.get_transfer_option. a broken rule throws InvalidOperationException with a message
public class TransferService
{
    private readonly Database db;
    private readonly CurrentDateService currentDateService;

    public TransferService(Database db, CurrentDateService currentDateService)
    {
        this.db = db;
        this.currentDateService = currentDateService;
    }

    // everything the rules of one transfer need
    private record TransferContext(HolidayAccount account, HolidayAccount? next, DateOnly today, decimal day_hours,
        decimal reserved_hours, TransferTotals totals, DateOnly? end_date, bool has_pending_transfer)
    {
        public TransferOption option() => AccountStatusService.get_transfer_option(account, next, today, day_hours,
            reserved_hours, totals, end_date, has_pending_transfer);
    }

    // a new pending transfer of `hours` from the account to the next holiday period of the same type
    public async Task<HolidayTransferRequest> request_transfer(Guid user_id, Guid account_id, decimal hours, string? comment)
    {
        check_hours(hours);
        await using var transaction = await db.BeginTransactionAsync();

        TransferContext context = await load(user_id, account_id, ignore_transfer_id: null);
        TransferOption option = context.option();
        if (!option.possible)
        {
            throw new InvalidOperationException(option.reason);
        }
        if (hours > option.max_hours)
        {
            throw new InvalidOperationException($"At most {option.max_hours:0.00} h of this balance can be transferred");
        }

        HolidayTransferRequest created = await db.transfer_requests.InsertWithOutputAsync(new HolidayTransferRequest
        {
            user_id = user_id,
            from_account_id = account_id,
            to_account_id = context.next!.id,
            holiday_type = context.account.holiday_type,
            hours = hours,
            deadline = option.deadline!.Value,
            comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            status = RequestStatus.Pending,
            created_at = DateTime.UtcNow
        });
        await transaction.CommitAsync();
        return created;
    }

    // the employee takes back a pending transfer request
    public async Task withdraw(Guid user_id, Guid id)
    {
        if (!await db.transfer_requests.AnyAsync(t => t.id == id && t.user_id == user_id))
        {
            throw new KeyNotFoundException("Transfer request not found");
        }
        int updated = await db.transfer_requests
            .Where(t => t.id == id && t.user_id == user_id && t.status == RequestStatus.Pending)
            .Set(t => t.status, RequestStatus.Withdrawn)
            .Set(t => t.decided_at, DateTime.UtcNow)
            .UpdateAsync();
        if (updated == 0)
        {
            throw new RequestStateException("Only a pending transfer request can be withdrawn. It was already approved or declined.");
        }
    }

    // the accountant approves: the written agreement. the rules are checked again (the balance or the date may have
    // changed since the request was sent), then the hours move from the account to the next one
    public async Task accept(Guid id, Guid accountant_id)
    {
        await using var transaction = await db.BeginTransactionAsync();

        HolidayTransferRequest? transfer = await db.transfer_requests
            .FirstOrDefaultAsync(t => t.id == id && t.status == RequestStatus.Pending);
        if (transfer == null)
        {
            throw new RequestStateException("This transfer request does not exist or was already handled.");
        }

        TransferContext context = await load(transfer.user_id, transfer.from_account_id, ignore_transfer_id: transfer.id);
        TransferOption option = context.option();
        if (!option.possible)
        {
            throw new InvalidOperationException(option.reason);
        }
        if (transfer.hours > option.max_hours)
        {
            throw new InvalidOperationException(
                $"Only {option.max_hours:0.00} h of this balance can be transferred now (requested {transfer.hours:0.00} h)");
        }
        if (context.next!.id != transfer.to_account_id)
        {
            throw new InvalidOperationException("The next holiday period of this balance has changed, decline the request");
        }

        // the status condition is repeated in the UPDATE: approved twice at the same time -> the hours move only once
        int updated = await db.transfer_requests
            .Where(t => t.id == id && t.status == RequestStatus.Pending)
            .Set(t => t.status, RequestStatus.Accepted)
            .Set(t => t.decided_at, DateTime.UtcNow)
            .Set(t => t.decided_by, accountant_id)
            .UpdateAsync();
        if (updated == 0)
        {
            throw new RequestStateException("This transfer request was already handled.");
        }

        // week 6: "transferred hours must be used before newly earned ones". both are in the same balance of the
        // next account and that balance is spent as one, so no earned hour can be used before a transferred one
        await change_transferred(context.account, -transfer.hours);
        await change_transferred(context.next, transfer.hours);
        await transaction.CommitAsync();
    }

    // the accountant declines, with an optional note for the employee
    public async Task decline(Guid id, Guid accountant_id, string? note)
    {
        int updated = await db.transfer_requests
            .Where(t => t.id == id && t.status == RequestStatus.Pending)
            .Set(t => t.status, RequestStatus.Rejected)
            .Set(t => t.decided_at, DateTime.UtcNow)
            .Set(t => t.decided_by, accountant_id)
            .Set(t => t.note, string.IsNullOrWhiteSpace(note) ? null : note.Trim())
            .UpdateAsync();
        if (updated == 0)
        {
            throw new RequestStateException("This transfer request does not exist or was already handled.");
        }
    }

    // pending transfer requests of all employees (accountant), the closest deadline first, with whether they can
    // still be approved
    public async Task<List<TransferRequestView>> get_pending()
    {
        List<HolidayTransferRequest> pending = await db.transfer_requests
            .Where(t => t.status == RequestStatus.Pending)
            .ToListAsync();
        List<TransferRequestView> views = await to_views(pending);

        foreach (TransferRequestView view in views)
        {
            HolidayTransferRequest transfer = pending.First(t => t.id == view.id);
            TransferOption option = (await load(transfer.user_id, transfer.from_account_id, transfer.id)).option();
            view.max_hours_now = option.max_hours;
            view.can_approve = option.possible && transfer.hours <= option.max_hours;
            view.not_approvable_reason = !option.possible ? option.reason
                : transfer.hours > option.max_hours ? $"Only {option.max_hours:0.00} h can be transferred now"
                : null;
        }
        return views.OrderBy(v => v.deadline).ThenBy(v => v.created_at).ToList();
    }

    // all transfer requests of the employee, newest first
    public async Task<List<TransferRequestView>> get_user_transfers(Guid user_id)
    {
        List<HolidayTransferRequest> transfers = await db.transfer_requests.Where(t => t.user_id == user_id).ToListAsync();
        return (await to_views(transfers)).OrderByDescending(v => v.created_at).ToList();
    }

    // next_month: a transfer that is still pending after its deadline can not be agreed anymore
    public static async Task decline_overdue(Database db, DateOnly today)
    {
        await db.transfer_requests
            .Where(t => t.status == RequestStatus.Pending && t.deadline < today)
            .Set(t => t.status, RequestStatus.Rejected)
            .Set(t => t.decided_at, DateTime.UtcNow)
            .Set(t => t.note, "The deadline passed before the transfer was approved")
            .UpdateAsync();
    }

    // the employee leaves: their remaining holiday is settled with the final salary, nothing is transferred.
    // returns how many pending requests were declined. must run inside the caller's transaction
    public static async Task<int> decline_for_leaving(Database db, Guid user_id, DateOnly last_day)
    {
        return await db.transfer_requests
            .Where(t => t.user_id == user_id && t.status == RequestStatus.Pending)
            .Set(t => t.status, RequestStatus.Rejected)
            .Set(t => t.decided_at, DateTime.UtcNow)
            .Set(t => t.note, $"The employment ends on {last_day:yyyy-MM-dd}, the remaining holiday is settled with the final salary")
            .UpdateAsync();
    }

    private async Task<TransferContext> load(Guid user_id, Guid account_id, Guid? ignore_transfer_id)
    {
        HolidayAccount? account =
            (HolidayAccount?)await db.law_holidays.FirstOrDefaultAsync(l => l.id == account_id && l.user_id == user_id)
            ?? await db.agreed_holidays.FirstOrDefaultAsync(a => a.id == account_id && a.user_id == user_id);
        if (account == null)
        {
            throw new KeyNotFoundException("Holiday balance not found");
        }

        DateOnly next_start = account.earning_period_end.AddDays(1);
        HolidayAccount? next = account.holiday_type == HolidayType.FerieEfterFerieloven
            ? await db.law_holidays.FirstOrDefaultAsync(l => l.user_id == user_id && l.earning_period_start == next_start)
            : await db.agreed_holidays.FirstOrDefaultAsync(a => a.user_id == user_id && a.earning_period_start == next_start);

        DateOnly today = await currentDateService.get_current_date();
        List<Schedule> schedules = await db.schedules.Where(s => s.user_id == user_id).ToListAsync();
        Schedule? schedule = Schedule.find_for_date(schedules, today)
                             ?? schedules.OrderByDescending(s => s.effective_from).FirstOrDefault();

        decimal reserved = await db.HolidayRequest
            .Where(r => r.account_id == account_id && r.RequestStatus == RequestStatus.Pending)
            .SumAsync(r => (decimal?)r.Hours) ?? 0;
        DateOnly? end_date = await db.users.Where(u => u.id == user_id).Select(u => u.end_date).FirstOrDefaultAsync();
        bool has_pending = await db.transfer_requests.AnyAsync(t => t.from_account_id == account_id
                                                                    && t.status == RequestStatus.Pending
                                                                    && (ignore_transfer_id == null || t.id != ignore_transfer_id));

        TransferTotals totals = TransferTotals.from_accepted(
            await db.transfer_requests
                .Where(t => (t.from_account_id == account_id || t.to_account_id == account_id) && t.status == RequestStatus.Accepted)
                .ToListAsync(),
            new[] { account_id })[account_id];

        return new TransferContext(account, next, today, schedule?.get_average_day_hours() ?? 0, reserved, totals,
            end_date, has_pending);
    }

    private async Task<List<TransferRequestView>> to_views(List<HolidayTransferRequest> transfers)
    {
        if (transfers.Count == 0)
        {
            return new List<TransferRequestView>();
        }

        List<Guid> user_ids = transfers.Select(t => t.user_id).Distinct().ToList();
        List<Guid> account_ids = transfers.SelectMany(t => new[] { t.from_account_id, t.to_account_id }).Distinct().ToList();
        Dictionary<Guid, HolidayAccount> accounts = (await db.law_holidays.Where(l => account_ids.Contains(l.id)).ToListAsync())
            .Cast<HolidayAccount>()
            .Concat(await db.agreed_holidays.Where(a => account_ids.Contains(a.id)).ToListAsync())
            .ToDictionary(a => a.id);
        Dictionary<Guid, string> names = await db.users.Where(u => user_ids.Contains(u.id))
            .ToDictionaryAsync(u => u.id, u => u.name);

        DateOnly today = await currentDateService.get_current_date();
        Dictionary<Guid, List<Schedule>> schedules = (await db.schedules.Where(s => user_ids.Contains(s.user_id)).ToListAsync())
            .GroupBy(s => s.user_id)
            .ToDictionary(g => g.Key, g => g.ToList());

        return transfers.Select(t =>
        {
            HolidayAccount from = accounts[t.from_account_id];
            HolidayAccount? to = accounts.GetValueOrDefault(t.to_account_id);
            List<Schedule> user_schedules = schedules.GetValueOrDefault(t.user_id, new List<Schedule>());
            Schedule? schedule = Schedule.find_for_date(user_schedules, today)
                                 ?? user_schedules.OrderByDescending(s => s.effective_from).FirstOrDefault();
            return new TransferRequestView
            {
                id = t.id,
                user_id = t.user_id,
                name = names.GetValueOrDefault(t.user_id, ""),
                holiday_type = t.holiday_type,
                earning_period_start = from.earning_period_start,
                earning_period_end = from.earning_period_end,
                usage_period_end = from.usage_period_end,
                to_usage_period_end = to?.usage_period_end ?? from.usage_period_end,
                hours = t.hours,
                day_hours = Math.Round(schedule?.get_average_day_hours() ?? 0, 2, MidpointRounding.AwayFromZero),
                deadline = t.deadline,
                comment = t.comment,
                status = t.status,
                created_at = t.created_at,
                decided_at = t.decided_at,
                note = t.note
            };
        }).ToList();
    }

    private static void check_hours(decimal hours)
    {
        if (hours <= 0)
        {
            throw new InvalidOperationException("Enter how many hours to transfer");
        }
        if (Math.Round(hours, 2) != hours)
        {
            throw new InvalidOperationException("Hours can have at most two decimals");
        }
    }

    // + hours received, - hours sent
    private async Task change_transferred(HolidayAccount account, decimal hours)
    {
        if (account.holiday_type == HolidayType.FerieEfterFerieloven)
        {
            await db.law_holidays.Where(l => l.id == account.id)
                .Set(l => l.transferred_holiday_hours, l => l.transferred_holiday_hours + hours)
                .UpdateAsync();
        }
        else
        {
            await db.agreed_holidays.Where(a => a.id == account.id)
                .Set(a => a.transferred_holiday_hours, a => a.transferred_holiday_hours + hours)
                .UpdateAsync();
        }
    }
}

using api;
using be;
using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

// the request (or cancellation) is not in the state the action needs anymore, e.g. it was already approved,
// declined or withdrawn (by another tab / the accountant). the controller answers 409 Conflict with the message
public class RequestStateException : Exception
{
    public RequestStateException(string message) : base(message)
    {
    }
}

public class RequestService
{
    private readonly Database db;
    private readonly CurrentDateService currentDateService;
    private readonly RequestValidator validator;

    public RequestService(Database db, CurrentDateService currentDateService, RequestValidator validator)
    {
        this.db = db;
        this.currentDateService = currentDateService;
        this.validator = validator;
    }

    // a new pending request. all holiday rules are checked by RequestValidator (InvalidOperationException if broken)
    public async Task<HolidayRequest> SendRequest(RequestDTO requestdto)
    {
        DateOnly start = DateOnly.ParseExact(requestdto.StartDate, "yyyy-MM-dd");
        DateOnly end = DateOnly.ParseExact(requestdto.EndDate, "yyyy-MM-dd");

        ValidatedRequest valid = await validator.validate(requestdto.user_id, requestdto.account_id, start, end,
            ignore_request_id: null, check_notice: true);

        var request = new HolidayRequest()
        {
            user_id = requestdto.user_id,
            account_id = requestdto.account_id,
            StartDate = requestdto.StartDate,
            EndDate = requestdto.EndDate,
            Hours = valid.hours,
            is_main_holiday = valid.is_main_holiday,
            created_at = requestdto.created_at,
            RequestStatus = RequestStatus.Pending
        };

        return await db.HolidayRequest.InsertWithOutputAsync(request);
    }

    // Edit of a pending request: the old request becomes Withdrawn and a new pending request with the new dates
    // (same account) is created, pointing to the old one (replaces_request_id), so the full history is kept.
    // the new dates go through all rules again; if they break one, nothing is changed (the transaction is rolled back).
    // throws KeyNotFoundException (not the user's request), RequestStateException (not pending anymore),
    // InvalidOperationException (a holiday rule)
    public async Task<HolidayRequest> EditRequest(Guid user_id, Guid request_id, DateOnly start, DateOnly end)
    {
        await using var transaction = await db.BeginTransactionAsync();

        HolidayRequest? old = await db.HolidayRequest.FirstOrDefaultAsync(r => r.id == request_id && r.user_id == user_id);
        if (old == null)
        {
            throw new KeyNotFoundException("Request not found");
        }

        // the status condition is in the UPDATE itself: if the accountant approved / declined it in the meantime,
        // nothing is updated. the old request is withdrawn first, so it does not count as an overlap or as reserved
        // hours when the new dates are checked
        DateTime now = DateTime.UtcNow;
        int updated = await db.HolidayRequest
            .Where(r => r.id == request_id && r.RequestStatus == RequestStatus.Pending)
            .Set(r => r.RequestStatus, RequestStatus.Withdrawn)
            .Set(r => r.updated_at, now)
            .UpdateAsync();
        if (updated == 0)
        {
            throw new RequestStateException(
                "Only a pending request can be changed. This request was already approved, declined or withdrawn.");
        }

        ValidatedRequest valid = await validator.validate(user_id, old.account_id, start, end,
            ignore_request_id: null, check_notice: true);

        HolidayRequest created = await db.HolidayRequest.InsertWithOutputAsync(new HolidayRequest
        {
            user_id = user_id,
            account_id = old.account_id,
            StartDate = start.ToString("yyyy-MM-dd"),
            EndDate = end.ToString("yyyy-MM-dd"),
            Hours = valid.hours,
            is_main_holiday = valid.is_main_holiday,
            created_at = now,
            RequestStatus = RequestStatus.Pending,
            replaces_request_id = old.id
        });

        await transaction.CommitAsync();
        return created;
    }

    // the employee takes back a pending request: it becomes Withdrawn, no new request is created.
    // nothing is given back, pending hours were only reserved, never spent
    public async Task WithdrawRequest(Guid user_id, Guid request_id)
    {
        bool exists = await db.HolidayRequest.AnyAsync(r => r.id == request_id && r.user_id == user_id);
        if (!exists)
        {
            throw new KeyNotFoundException("Request not found");
        }

        int updated = await db.HolidayRequest
            .Where(r => r.id == request_id && r.user_id == user_id && r.RequestStatus == RequestStatus.Pending)
            .Set(r => r.RequestStatus, RequestStatus.Withdrawn)
            .Set(r => r.updated_at, DateTime.UtcNow)
            .UpdateAsync();
        if (updated == 0)
        {
            throw new RequestStateException(
                "Only a pending request can be withdrawn. This request was already approved, declined or withdrawn.");
        }
    }

    // hours a holiday from start to end (both included) costs. a day costs nothing if it is
    // a Saturday / Sunday, a public holiday or company day off, or a day the employee does not work (part time).
    // otherwise it costs the hours of that weekday in the schedule that applies on that date
    // (schedule history: a holiday after a schedule change costs the hours of the new schedule)
    public static decimal calculate_holiday_hours(DateOnly start, DateOnly end, List<Schedule> schedules, HashSet<DateOnly> days_off)
    {
        decimal hours = 0;
        for (DateOnly day = start; day <= end; day = day.AddDays(1))
        {
            if (days_off.Contains(day))
            {
                continue;
            }

            Schedule? schedule = Schedule.find_for_date(schedules, day);
            if (schedule == null)
            {
                // the user did not work here yet
                continue;
            }

            // Schedule.get_hours returns 0 for Saturday, Sunday and the employee's days off
            hours += schedule.get_hours(day.DayOfWeek);
        }
        return hours;
    }

    // pending requests of all employees (accountant)
    public async Task<List<HolidayRequestUser>> GetRequests() {
        return await db.HolidayRequest
            .Where(r => r.RequestStatus == RequestStatus.Pending)
            .Join(db.users, r => r.user_id, u => u.id, (r, u) => new {r, u})
            .SelectMany(
                x => db.law_holidays.LeftJoin(l => l.id == x.r.account_id),
                (x, law) => new {x.r, x.u, law}
            )
            .SelectMany(
                x => db.agreed_holidays.LeftJoin(a => a.id == x.r.account_id),
                (x, agreed) => new HolidayRequestUser
                {
                    Id = x.r.id,
                    Name = x.u.name,
                    StartDate = x.r.StartDate,
                    EndDate = x.r.EndDate,
                    Hours = x.r.Hours,
                    IsMainHoliday = x.r.is_main_holiday,
                    HolidayType = x.law != null ? x.law.holiday_type : agreed.holiday_type,
                    Status = x.r.RequestStatus
                }
            ).ToListAsync();
    }

    // pending requests of the employee (Ongoing Requests tab), newest first
    public async Task<List<OngoingHolidayRequest>> GetOngoingRequests(Guid userId)
    {
        List<OngoingHolidayRequest> requests = await get_user_requests(userId, RequestStatus.Pending);
        return requests.OrderByDescending(r => r.CreatedAt).ToList();
    }

    // approved holidays of the employee that are not over yet (Approved Holidays tab), the next one first.
    // Started = it can not be cancelled anymore, HasPendingCancellation = a cancellation waits for the accountant
    public async Task<List<OngoingHolidayRequest>> GetApprovedRequests(Guid userId)
    {
        DateOnly today = await currentDateService.get_current_date();
        string today_text = today.ToString("yyyy-MM-dd");

        List<OngoingHolidayRequest> requests = (await get_user_requests(userId, RequestStatus.Accepted))
            // dates are "yyyy-MM-dd", so comparing the text compares the dates
            .Where(r => string.CompareOrdinal(r.EndDate, today_text) >= 0)
            .ToList();

        List<Guid> ids = requests.Select(r => r.Id).ToList();
        HashSet<Guid> with_pending_cancellation = (await db.cancellation_requests
                .Where(c => ids.Contains(c.holiday_request_id) && c.status == RequestStatus.Pending)
                .Select(c => c.holiday_request_id)
                .ToListAsync())
            .ToHashSet();

        foreach (OngoingHolidayRequest r in requests)
        {
            r.HasPendingCancellation = with_pending_cancellation.Contains(r.Id);
            r.Started = string.CompareOrdinal(r.StartDate, today_text) <= 0;
        }

        return requests.OrderBy(r => r.StartDate).ToList();
    }

    private async Task<List<OngoingHolidayRequest>> get_user_requests(Guid userId, RequestStatus status)
    {
        var lawRequests = await db.HolidayRequest
            .Where(r => r.user_id == userId && r.RequestStatus == status)
            .Join(db.law_holidays, r => r.account_id, a => a.id, (r, a) => new OngoingHolidayRequest
            {
                Id = r.id, StartDate = r.StartDate, EndDate = r.EndDate, Hours = r.Hours,
                CreatedAt = r.created_at, HolidayKind = "law", RequestStatus = r.RequestStatus,
                IsMainHoliday = r.is_main_holiday
            }).ToListAsync();

        var agreedRequests = await db.HolidayRequest
            .Where(r => r.user_id == userId && r.RequestStatus == status)
            .Join(db.agreed_holidays, r => r.account_id, a => a.id, (r, a) => new OngoingHolidayRequest
            {
                Id = r.id, StartDate = r.StartDate, EndDate = r.EndDate, Hours = r.Hours,
                CreatedAt = r.created_at, HolidayKind = "agreed", RequestStatus = r.RequestStatus,
                IsMainHoliday = r.is_main_holiday
            }).ToListAsync();

        return lawRequests.Concat(agreedRequests).ToList();
    }

    // only a PENDING request can be declined. an accepted request has already added its hours to used_holiday_hours,
    // declining it here would leave those hours spent (cancelling approved holiday needs its own cancellation request).
    // returns false if the request does not exist or is not pending anymore
    public async Task<bool> DeclineRequest(Guid id) {
        int updated = await db.HolidayRequest
            .Where(r => r.id == id && r.RequestStatus == RequestStatus.Pending)
            .Set(r => r.RequestStatus, RequestStatus.Rejected)
            .Set(r => r.decided_at, DateTime.UtcNow)
            .UpdateAsync();
        return updated > 0;
    }

    // the request is checked again before it is approved: since it was sent the balance may have changed
    // (other approvals, a schedule change, a new company day off changes its cost). the notice is not checked again.
    // throws RequestStateException (not pending anymore) or InvalidOperationException (a holiday rule is broken now)
    public async Task AcceptRequest(Guid id) {
        await using var transaction = await db.BeginTransactionAsync();
        HolidayRequest? request = await db.HolidayRequest.FirstOrDefaultAsync(r => r.id == id && r.RequestStatus == RequestStatus.Pending);
        if (request == null)
        {
            throw new RequestStateException(
                "Only a pending request can be approved. This request does not exist or was already handled.");
        }

        ValidatedRequest valid = await validator.validate(request.user_id, request.account_id,
            DateOnly.ParseExact(request.StartDate, "yyyy-MM-dd"), DateOnly.ParseExact(request.EndDate, "yyyy-MM-dd"),
            ignore_request_id: request.id, check_notice: false);

        // the status condition is repeated in the UPDATE: if another accept/decline/withdraw changed the request after
        // it was read above, nothing is updated and the hours are not added a second time.
        // the hours are the recalculated ones, so the balance is charged what the holiday costs today
        int updated = await db.HolidayRequest
            .Where(r => r.id == id && r.RequestStatus == RequestStatus.Pending)
            .Set(r => r.RequestStatus, RequestStatus.Accepted)
            .Set(r => r.decided_at, DateTime.UtcNow)
            .Set(r => r.Hours, valid.hours)
            .UpdateAsync();
        if (updated == 0)
        {
            throw new RequestStateException(
                "Only a pending request can be approved. This request does not exist or was already handled.");
        }

        await change_used_hours(valid.account, valid.hours);
        await transaction.CommitAsync();
    }

    // the employee asks to cancel an approved holiday. only before it starts: a started holiday can never be
    // interrupted. the holiday stays approved until the accountant approves the cancellation
    public async Task<HolidayCancellationRequest> RequestCancellation(Guid user_id, Guid request_id, string? reason)
    {
        HolidayRequest? request = await db.HolidayRequest.FirstOrDefaultAsync(r => r.id == request_id && r.user_id == user_id);
        if (request == null)
        {
            throw new KeyNotFoundException("Holiday not found");
        }
        if (request.RequestStatus != RequestStatus.Accepted)
        {
            throw new RequestStateException("Only an approved holiday can be cancelled");
        }

        DateOnly today = await currentDateService.get_current_date();
        if (DateOnly.ParseExact(request.StartDate, "yyyy-MM-dd") <= today)
        {
            throw new RequestStateException("This holiday has already started, it can not be cancelled anymore");
        }

        if (await db.cancellation_requests.AnyAsync(c => c.holiday_request_id == request_id && c.status == RequestStatus.Pending))
        {
            throw new RequestStateException("A cancellation of this holiday is already waiting for the accountant");
        }

        string? trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return await db.cancellation_requests.InsertWithOutputAsync(new HolidayCancellationRequest
        {
            holiday_request_id = request_id,
            user_id = user_id,
            reason = trimmed,
            status = RequestStatus.Pending,
            created_at = DateTime.UtcNow
        });
    }

    // pending cancellation requests of all employees (accountant), the oldest first
    public async Task<List<CancellationRequestView>> GetCancellationRequests()
    {
        return await db.cancellation_requests
            .Where(c => c.status == RequestStatus.Pending)
            .Join(db.HolidayRequest, c => c.holiday_request_id, r => r.id, (c, r) => new {c, r})
            .Join(db.users, x => x.c.user_id, u => u.id, (x, u) => new {x.c, x.r, u})
            .SelectMany(
                x => db.law_holidays.LeftJoin(l => l.id == x.r.account_id),
                (x, law) => new {x.c, x.r, x.u, law}
            )
            .SelectMany(
                x => db.agreed_holidays.LeftJoin(a => a.id == x.r.account_id),
                (x, agreed) => new CancellationRequestView
                {
                    Id = x.c.id,
                    HolidayRequestId = x.r.id,
                    Name = x.u.name,
                    StartDate = x.r.StartDate,
                    EndDate = x.r.EndDate,
                    Hours = x.r.Hours,
                    HolidayType = x.law != null ? x.law.holiday_type : agreed.holiday_type,
                    Reason = x.c.reason,
                    CreatedAt = x.c.created_at
                })
            .OrderBy(v => v.CreatedAt)
            .ToListAsync();
    }

    // the holiday becomes Cancelled and its hours are given back to the account it was spent from.
    // not possible anymore once the holiday has started (then the accountant can only decline the cancellation)
    public async Task AcceptCancellation(Guid id)
    {
        await using var transaction = await db.BeginTransactionAsync();

        HolidayCancellationRequest? cancellation = await db.cancellation_requests
            .FirstOrDefaultAsync(c => c.id == id && c.status == RequestStatus.Pending);
        if (cancellation == null)
        {
            throw new RequestStateException("This cancellation request does not exist or was already handled.");
        }

        HolidayRequest request = await db.HolidayRequest.FirstAsync(r => r.id == cancellation.holiday_request_id);
        if (request.RequestStatus != RequestStatus.Accepted)
        {
            throw new RequestStateException("The holiday is not approved anymore, it can not be cancelled.");
        }

        DateOnly today = await currentDateService.get_current_date();
        if (DateOnly.ParseExact(request.StartDate, "yyyy-MM-dd") <= today)
        {
            throw new RequestStateException(
                "The holiday has already started, it can not be cancelled anymore. Decline the cancellation instead.");
        }

        DateTime now = DateTime.UtcNow;
        int cancellation_updated = await db.cancellation_requests
            .Where(c => c.id == id && c.status == RequestStatus.Pending)
            .Set(c => c.status, RequestStatus.Accepted)
            .Set(c => c.decided_at, now)
            .UpdateAsync();
        int request_updated = await db.HolidayRequest
            .Where(r => r.id == request.id && r.RequestStatus == RequestStatus.Accepted)
            .Set(r => r.RequestStatus, RequestStatus.Cancelled)
            .Set(r => r.updated_at, now)
            .UpdateAsync();
        if (cancellation_updated == 0 || request_updated == 0)
        {
            throw new RequestStateException("This cancellation request was already handled.");
        }

        // give the hours back: the approved holiday added exactly request.Hours to used_holiday_hours
        HolidayAccount account =
            (HolidayAccount?)await db.law_holidays.FirstOrDefaultAsync(l => l.id == request.account_id)
            ?? await db.agreed_holidays.FirstAsync(a => a.id == request.account_id);
        await change_used_hours(account, -request.Hours);

        await transaction.CommitAsync();
    }

    // the holiday stays approved, the declined cancellation stays in the history
    public async Task DeclineCancellation(Guid id)
    {
        int updated = await db.cancellation_requests
            .Where(c => c.id == id && c.status == RequestStatus.Pending)
            .Set(c => c.status, RequestStatus.Rejected)
            .Set(c => c.decided_at, DateTime.UtcNow)
            .UpdateAsync();
        if (updated == 0)
        {
            throw new RequestStateException("This cancellation request does not exist or was already handled.");
        }
    }

    // + hours when a holiday is approved, - hours when it is cancelled
    private async Task change_used_hours(HolidayAccount account, decimal hours)
    {
        if (account.holiday_type == HolidayType.FerieEfterFerieloven)
        {
            await db.law_holidays.Where(l => l.id == account.id)
                .Set(l => l.used_holiday_hours, l => l.used_holiday_hours + hours)
                .UpdateAsync();
        }
        else
        {
            await db.agreed_holidays.Where(a => a.id == account.id)
                .Set(a => a.used_holiday_hours, a => a.used_holiday_hours + hours)
                .UpdateAsync();
        }
    }
}

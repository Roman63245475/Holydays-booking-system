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
            initiated_by = CancellationInitiator.Employee,
            created_at = DateTime.UtcNow
        });
    }

    // pending cancellation requests of all employees (accountant), the oldest first
    public async Task<List<CancellationRequestView>> GetCancellationRequests()
    {
        return await db.cancellation_requests
            .Where(c => c.status == RequestStatus.Pending && c.initiated_by == CancellationInitiator.Employee)
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

    // the employee's cancellation is approved: the holiday becomes Cancelled and its hours are given back.
    // not possible anymore once the holiday has started (then the accountant can only decline the cancellation)
    public async Task AcceptCancellation(Guid id, Guid accountant_id)
    {
        await using var transaction = await db.BeginTransactionAsync();

        HolidayCancellationRequest? cancellation = await db.cancellation_requests
            .FirstOrDefaultAsync(c => c.id == id && c.status == RequestStatus.Pending);
        if (cancellation == null)
        {
            throw new RequestStateException("This cancellation request does not exist or was already handled.");
        }

        HolidayRequest request = await db.HolidayRequest.FirstAsync(r => r.id == cancellation.holiday_request_id);
        await check_can_be_cancelled(request,
            "The holiday has already started, it can not be cancelled anymore. Decline the cancellation instead.");

        await approve_employee_cancellation(cancellation.id, request, accountant_id);
        await transaction.CommitAsync();
    }

    // the holiday stays approved, the declined cancellation stays in the history
    public async Task DeclineCancellation(Guid id, Guid accountant_id)
    {
        int updated = await db.cancellation_requests
            .Where(c => c.id == id && c.status == RequestStatus.Pending)
            .Set(c => c.status, RequestStatus.Rejected)
            .Set(c => c.decided_at, DateTime.UtcNow)
            .Set(c => c.decided_by, accountant_id)
            .UpdateAsync();
        if (updated == 0)
        {
            throw new RequestStateException("This cancellation request does not exist or was already handled.");
        }
    }

    // the accountant cancels an approved holiday on the employer's decision (05.11 § 10): only before it starts,
    // only in force-majeure-like situations (the accountant confirms it), the reason is recorded and the
    // employee's financial loss is covered by payroll (the system stores no money).
    // if the employee has already asked to cancel this holiday, their request is approved instead: the holiday is
    // cancelled on the EMPLOYEE's wish, so no force majeure / employer reason is needed.
    // returns true if the employee's request was approved, false if it was cancelled as the employer's decision.
    // throws ArgumentException (reason / confirmation missing), RequestStateException (not approved / started)
    public async Task<bool> EmployerCancel(Guid request_id, Guid accountant_id, string? reason, bool confirmed)
    {
        await using var transaction = await db.BeginTransactionAsync();

        HolidayRequest? request = await db.HolidayRequest.FirstOrDefaultAsync(r => r.id == request_id);
        if (request == null)
        {
            throw new KeyNotFoundException("Holiday not found");
        }
        await check_can_be_cancelled(request, "The holiday has already started, it can not be cancelled anymore.");

        HolidayCancellationRequest? pending = await db.cancellation_requests
            .FirstOrDefaultAsync(c => c.holiday_request_id == request_id && c.status == RequestStatus.Pending);
        if (pending != null)
        {
            await approve_employee_cancellation(pending.id, request, accountant_id);
            await transaction.CommitAsync();
            return true;
        }

        string trimmed = check_employer_decision(reason, confirmed, "cancelled",
            "An approved holiday can only be cancelled by the employer in a force-majeure-like situation, " +
            "after consulting the employee and with their financial loss covered. Confirm this to cancel it.");

        DateTime now = DateTime.UtcNow;
        await record_employer_cancellation(request, accountant_id, trimmed, now);
        await cancel_holiday(request, now);

        await transaction.CommitAsync();
        return false;
    }

    // the accountant moves an approved holiday to new dates on the employer's decision (05.11 § 10): only before it
    // starts, only in a force-majeure-like situation with the new dates agreed with the employee (confirmed), the
    // reason is recorded. it is the employer's cancellation of the old holiday + a new approved holiday on the new
    // dates (same account) that points to it (replaces_request_id), all in one transaction: if the new dates break a
    // rule, nothing changes. the new dates are checked like any request (period, overlap, balance, debt, 31 August)
    // but without the notice: the move is urgent and agreed with the employee.
    // throws KeyNotFoundException, ArgumentException (reason / confirmation / same dates),
    // RequestStateException (not approved / started / the employee asked to cancel), InvalidOperationException (rule)
    public async Task<HolidayRequest> EmployerReschedule(Guid request_id, Guid accountant_id, DateOnly start, DateOnly end,
        string? reason, bool confirmed)
    {
        await using var transaction = await db.BeginTransactionAsync();

        HolidayRequest? request = await db.HolidayRequest.FirstOrDefaultAsync(r => r.id == request_id);
        if (request == null)
        {
            throw new KeyNotFoundException("Holiday not found");
        }
        await check_can_be_cancelled(request, "The holiday has already started, it can not be moved anymore.");

        // the employee wants to cancel it, moving it would ignore that: the accountant must answer the employee first
        if (await db.cancellation_requests.AnyAsync(c => c.holiday_request_id == request_id && c.status == RequestStatus.Pending))
        {
            throw new RequestStateException(
                "The employee has asked to cancel this holiday. Approve or decline their cancellation request first.");
        }

        string trimmed = check_employer_decision(reason, confirmed, "moved",
            "An approved holiday can only be moved by the employer in a force-majeure-like situation, with the new " +
            "dates agreed with the employee and their financial loss covered. Confirm this to move it.");

        if (start.ToString("yyyy-MM-dd") == request.StartDate && end.ToString("yyyy-MM-dd") == request.EndDate)
        {
            throw new ArgumentException("The new dates are the same as the current ones");
        }

        // the old holiday is cancelled first: its hours are back and it no longer counts as an overlap,
        // so the new dates are checked against the balance without it
        DateTime now = DateTime.UtcNow;
        await record_employer_cancellation(request, accountant_id, trimmed, now);
        await cancel_holiday(request, now);

        ValidatedRequest valid = await validator.validate(request.user_id, request.account_id, start, end,
            ignore_request_id: null, check_notice: false);

        HolidayRequest moved = await db.HolidayRequest.InsertWithOutputAsync(new HolidayRequest
        {
            user_id = request.user_id,
            account_id = request.account_id,
            StartDate = start.ToString("yyyy-MM-dd"),
            EndDate = end.ToString("yyyy-MM-dd"),
            Hours = valid.hours,
            is_main_holiday = valid.is_main_holiday,
            created_at = now,
            decided_at = now,
            RequestStatus = RequestStatus.Accepted,
            replaces_request_id = request.id
        });
        await change_used_hours(valid.account, valid.hours);

        await transaction.CommitAsync();
        return moved;
    }

    // the reason of an employer's decision is required (the law asks to record it) and the accountant must confirm the
    // force-majeure-like situation. returns the trimmed reason
    private static string check_employer_decision(string? reason, bool confirmed, string action, string confirm_message)
    {
        string trimmed = reason?.Trim() ?? "";
        if (trimmed.Length < MIN_EMPLOYER_REASON_LENGTH)
        {
            throw new ArgumentException(
                $"Write why the holiday is {action} (at least {MIN_EMPLOYER_REASON_LENGTH} characters), the reason must be recorded");
        }
        if (!confirmed)
        {
            throw new ArgumentException(confirm_message);
        }
        return trimmed;
    }

    // the employer's cancellation is saved directly as approved, with the reason. must run inside the caller's transaction
    private async Task record_employer_cancellation(HolidayRequest request, Guid accountant_id, string reason, DateTime now)
    {
        await db.cancellation_requests.InsertAsync(() => new HolidayCancellationRequest
        {
            holiday_request_id = request.id,
            user_id = request.user_id,
            reason = reason,
            status = RequestStatus.Accepted,
            initiated_by = CancellationInitiator.Employer,
            created_at = now,
            decided_at = now,
            decided_by = accountant_id
        });
    }

    public const int MIN_EMPLOYER_REASON_LENGTH = 10;

    // approved holidays of all employees that are not over yet (accountant's Approved Holidays tab), the next one first
    public async Task<List<ApprovedHolidayView>> GetAllApprovedRequests()
    {
        DateOnly today = await currentDateService.get_current_date();
        string today_text = today.ToString("yyyy-MM-dd");

        List<ApprovedHolidayView> holidays = (await db.HolidayRequest
                .Where(r => r.RequestStatus == RequestStatus.Accepted)
                .Join(db.users, r => r.user_id, u => u.id, (r, u) => new {r, u})
                .SelectMany(
                    x => db.law_holidays.LeftJoin(l => l.id == x.r.account_id),
                    (x, law) => new {x.r, x.u, law}
                )
                .SelectMany(
                    x => db.agreed_holidays.LeftJoin(a => a.id == x.r.account_id),
                    (x, agreed) => new ApprovedHolidayView
                    {
                        Id = x.r.id,
                        Name = x.u.name,
                        StartDate = x.r.StartDate,
                        EndDate = x.r.EndDate,
                        Hours = x.r.Hours,
                        IsMainHoliday = x.r.is_main_holiday,
                        HolidayType = x.law != null ? x.law.holiday_type : agreed.holiday_type
                    })
                .ToListAsync())
            // dates are "yyyy-MM-dd", so comparing the text compares the dates
            .Where(h => string.CompareOrdinal(h.EndDate, today_text) >= 0)
            .ToList();

        List<Guid> ids = holidays.Select(h => h.Id).ToList();
        Dictionary<Guid, string?> pending_reasons = (await db.cancellation_requests
                .Where(c => ids.Contains(c.holiday_request_id) && c.status == RequestStatus.Pending)
                .Select(c => new { c.holiday_request_id, c.reason })
                .ToListAsync())
            .ToDictionary(c => c.holiday_request_id, c => c.reason);

        foreach (ApprovedHolidayView h in holidays)
        {
            h.Started = string.CompareOrdinal(h.StartDate, today_text) <= 0;
            h.HasPendingCancellation = pending_reasons.ContainsKey(h.Id);
            h.PendingCancellationReason = pending_reasons.GetValueOrDefault(h.Id);
        }

        return holidays.OrderBy(h => h.StartDate).ThenBy(h => h.Name).ToList();
    }

    // holidays of the employee the accountant cancelled or moved on the employer's decision, with the reason, so the
    // employee sees what happened to their planned holiday. a holiday was MOVED if a request replaces it
    // (EmployerReschedule), otherwise it was cancelled. shown while the old or the new dates are not over yet
    public async Task<List<CancelledHolidayView>> GetChangedByEmployer(Guid userId)
    {
        DateOnly today = await currentDateService.get_current_date();
        string today_text = today.ToString("yyyy-MM-dd");

        List<CancelledHolidayView> rows = await db.cancellation_requests
            .Where(c => c.user_id == userId && c.initiated_by == CancellationInitiator.Employer
                        && c.status == RequestStatus.Accepted)
            .Join(db.HolidayRequest, c => c.holiday_request_id, r => r.id, (c, r) => new {c, r})
            .SelectMany(
                x => db.law_holidays.LeftJoin(l => l.id == x.r.account_id),
                (x, law) => new CancelledHolidayView
                {
                    Id = x.r.id,
                    StartDate = x.r.StartDate,
                    EndDate = x.r.EndDate,
                    Hours = x.r.Hours,
                    HolidayKind = law != null ? "law" : "agreed",
                    Reason = x.c.reason,
                    CancelledAt = x.c.created_at
                })
            .ToListAsync();

        List<Guid> ids = rows.Select(r => r.Id).ToList();
        Dictionary<Guid, HolidayRequest> moved_to = (await db.HolidayRequest
                .Where(r => r.replaces_request_id != null && ids.Contains(r.replaces_request_id.Value))
                .ToListAsync())
            .ToDictionary(r => r.replaces_request_id!.Value);

        foreach (CancelledHolidayView row in rows)
        {
            if (moved_to.TryGetValue(row.Id, out HolidayRequest? to))
            {
                row.MovedToStartDate = to.StartDate;
                row.MovedToEndDate = to.EndDate;
                row.MovedToHours = to.Hours;
            }
        }

        // dates are "yyyy-MM-dd", so comparing the text compares the dates
        return rows
            .Where(v => string.CompareOrdinal(v.EndDate, today_text) >= 0
                        || (v.MovedToEndDate != null && string.CompareOrdinal(v.MovedToEndDate, today_text) >= 0))
            .OrderBy(v => v.StartDate)
            .ToList();
    }

    // the holiday history of the user: every request that is not pending anymore, newest first. the note is built
    // from what is already stored: the request that replaced it (Edit by the employee or a move by the accountant,
    // replaces_request_id) and its cancellation requests (who cancelled it and why)
    public async Task<List<HolidayHistoryView>> GetHistory(Guid userId)
    {
        List<HolidayRequest> requests = await db.HolidayRequest
            .Where(r => r.user_id == userId && r.RequestStatus != RequestStatus.Pending)
            .ToListAsync();
        // all requests of the user (also pending ones): a pending request can be the new version of an edited one
        Dictionary<Guid, HolidayRequest> by_id = (await db.HolidayRequest.Where(r => r.user_id == userId).ToListAsync())
            .ToDictionary(r => r.id);
        Dictionary<Guid, HolidayRequest> replaced_by = by_id.Values
            .Where(r => r.replaces_request_id != null)
            .GroupBy(r => r.replaces_request_id!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.created_at).First());
        Dictionary<Guid, List<HolidayCancellationRequest>> cancellations = (await db.cancellation_requests
                .Where(c => c.user_id == userId)
                .ToListAsync())
            .GroupBy(c => c.holiday_request_id)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.created_at).ToList());

        List<Guid> account_ids = requests.Select(r => r.account_id).Distinct().ToList();
        HashSet<Guid> law_accounts = (await db.law_holidays
                .Where(l => account_ids.Contains(l.id))
                .Select(l => l.id)
                .ToListAsync())
            .ToHashSet();

        return requests
            .Select(r => new HolidayHistoryView
            {
                id = r.id,
                start_date = r.StartDate,
                end_date = r.EndDate,
                hours = r.Hours,
                holiday_type = law_accounts.Contains(r.account_id) ? HolidayType.FerieEfterFerieloven : HolidayType.FerieUge,
                is_main_holiday = r.is_main_holiday,
                status = r.RequestStatus,
                created_at = r.created_at,
                decided_at = r.updated_at ?? r.decided_at,
                note = history_note(r, by_id, replaced_by, cancellations.GetValueOrDefault(r.id, new List<HolidayCancellationRequest>()))
            })
            .OrderByDescending(h => h.created_at)
            .ToList();
    }

    private static string? history_note(HolidayRequest r, Dictionary<Guid, HolidayRequest> by_id,
        Dictionary<Guid, HolidayRequest> replaced_by, List<HolidayCancellationRequest> cancellations)
    {
        replaced_by.TryGetValue(r.id, out HolidayRequest? successor);
        string successor_dates = successor != null ? $"{successor.StartDate} – {successor.EndDate}" : "";
        HolidayCancellationRequest? accepted = cancellations.FirstOrDefault(c => c.status == RequestStatus.Accepted);

        switch (r.RequestStatus)
        {
            case RequestStatus.Rejected:
                return "Declined by the accountant";
            case RequestStatus.Withdrawn:
                // Edit: the old request is withdrawn and a new one with the new dates replaces it
                return successor != null ? $"You changed the dates to {successor_dates}" : "Withdrawn by you";
            case RequestStatus.Cancelled:
                if (accepted?.initiated_by == CancellationInitiator.Employer)
                {
                    string reason = string.IsNullOrWhiteSpace(accepted.reason) ? "" : $": {accepted.reason}";
                    return successor != null
                        ? $"Moved to {successor_dates} by the accountant{reason}"
                        : $"Cancelled by the accountant{reason}";
                }
                string own_reason = string.IsNullOrWhiteSpace(accepted?.reason) ? "" : $" ({accepted!.reason})";
                return $"Cancelled on your request{own_reason}";
            case RequestStatus.Accepted:
                List<string> notes = new List<string>();
                if (r.replaces_request_id != null && by_id.TryGetValue(r.replaces_request_id.Value, out HolidayRequest? before))
                {
                    notes.Add(before.RequestStatus == RequestStatus.Cancelled
                        ? $"Moved here by the accountant from {before.StartDate} – {before.EndDate}"
                        : $"Changed from {before.StartDate} – {before.EndDate}");
                }
                HolidayCancellationRequest? last = cancellations.FirstOrDefault();
                if (last?.status == RequestStatus.Pending)
                {
                    notes.Add("You asked to cancel it, waiting for the accountant");
                }
                else if (last?.status == RequestStatus.Rejected)
                {
                    notes.Add("Your cancellation request was declined");
                }
                return notes.Count > 0 ? string.Join(". ", notes) : null;
            default:
                return null;
        }
    }

    // an approved holiday can be cancelled only while it is approved and has not started (05.11 § 10)
    private async Task check_can_be_cancelled(HolidayRequest request, string started_message)
    {
        if (request.RequestStatus != RequestStatus.Accepted)
        {
            throw new RequestStateException("The holiday is not approved anymore, it can not be cancelled.");
        }

        DateOnly today = await currentDateService.get_current_date();
        if (DateOnly.ParseExact(request.StartDate, "yyyy-MM-dd") <= today)
        {
            throw new RequestStateException(started_message);
        }
    }

    // must run inside the caller's transaction
    private async Task approve_employee_cancellation(Guid cancellation_id, HolidayRequest request, Guid accountant_id)
    {
        DateTime now = DateTime.UtcNow;
        int updated = await db.cancellation_requests
            .Where(c => c.id == cancellation_id && c.status == RequestStatus.Pending)
            .Set(c => c.status, RequestStatus.Accepted)
            .Set(c => c.decided_at, now)
            .Set(c => c.decided_by, accountant_id)
            .UpdateAsync();
        if (updated == 0)
        {
            throw new RequestStateException("This cancellation request was already handled.");
        }
        await cancel_holiday(request, now);
    }

    // the holiday becomes Cancelled and exactly the hours its approval added to used_holiday_hours are given back.
    // the status condition is in the UPDATE: if it was cancelled in the meantime, nothing is given back twice.
    // must run inside the caller's transaction
    private async Task cancel_holiday(HolidayRequest request, DateTime now)
    {
        int updated = await db.HolidayRequest
            .Where(r => r.id == request.id && r.RequestStatus == RequestStatus.Accepted)
            .Set(r => r.RequestStatus, RequestStatus.Cancelled)
            .Set(r => r.updated_at, now)
            .UpdateAsync();
        if (updated == 0)
        {
            throw new RequestStateException("The holiday was already cancelled.");
        }

        HolidayAccount account =
            (HolidayAccount?)await db.law_holidays.FirstOrDefaultAsync(l => l.id == request.account_id)
            ?? await db.agreed_holidays.FirstAsync(a => a.id == request.account_id);
        await change_used_hours(account, -request.Hours);
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

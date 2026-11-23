using api;
using be;
using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

public class RequestService
{
    private readonly Database db;
    private readonly UserService userService;
    private readonly CurrentDateService currentDateService;

    public RequestService(Database db, UserService userService, CurrentDateService currentDateService)
    {
        this.db = db;
        this.userService = userService;
        this.currentDateService = currentDateService;
    }

    public async Task<HolidayRequest> SendRequest(RequestDTO requestdto)
    {
        // the account must belong to the user and must not be expired. "active" (still earning) does not matter here:
        // week 6 can only be used after its earning year, and weeks 1-5 also 4 months after their earning period ended
        HolidayAccount? account =
            (HolidayAccount?)await db.law_holidays.FirstOrDefaultAsync(l =>
                l.id == requestdto.account_id && l.user_id == requestdto.user_id && l.usable)
            ?? await db.agreed_holidays.FirstOrDefaultAsync(a =>
                a.id == requestdto.account_id && a.user_id == requestdto.user_id && a.usable);
        if (account == null)
        {
            throw new InvalidOperationException("No usable holiday account was found for this user");
        }

        // the holiday itself (not the day the request is sent) must lie inside the usage period of the account,
        // so a holiday can be requested long in advance as long as its dates fit
        DateOnly start = DateOnly.ParseExact(requestdto.StartDate, "yyyy-MM-dd");
        DateOnly end = DateOnly.ParseExact(requestdto.EndDate, "yyyy-MM-dd");

        // a usable account can still contain days that are already over (e.g. law account in March: Sep - Feb are past),
        // so the holiday must not start before today. "today" is the simulated date (time simulator), not DateTime.Now.
        // TODO: replaced by the notice rule later (start >= today + 1 month, 3 months for the main holiday)
        DateOnly today = await currentDateService.get_current_date();
        if (start < today)
        {
            throw new InvalidOperationException($"A holiday can not start in the past (today is {today:yyyy-MM-dd})");
        }

        if (!account.covers(start, end))
        {
            throw new InvalidOperationException(
                $"This balance can only be used from {account.usage_period_start:yyyy-MM-dd} to {account.usage_period_end:yyyy-MM-dd}");
        }

        // the cost of the holiday is calculated here, never taken from the client:
        // every day costs the hours of that weekday in the employee's own schedule that applies on that day
        List<Schedule> schedules = await db.schedules.Where(s => s.user_id == requestdto.user_id).ToListAsync();
        HashSet<DateOnly> days_off = (await db.days_off
                .Where(d => d.date >= start && d.date <= end)
                .Select(d => d.date)
                .ToListAsync())
            .ToHashSet();

        decimal hours = calculate_holiday_hours(start, end, schedules, days_off);
        if (hours == 0)
        {
            throw new InvalidOperationException(
                "The selected days contain no working days (weekends, public holidays and your days off are not holiday)");
        }

        await check_balance(account, hours, schedules, today);

        var request = new HolidayRequest()
        {
            user_id = requestdto.user_id,
            account_id = requestdto.account_id,
            StartDate = requestdto.StartDate,
            EndDate = requestdto.EndDate,
            Hours = hours,
            created_at = requestdto.created_at,
            RequestStatus = requestdto.RequestStatus
        };

        return await db.HolidayRequest.InsertWithOutputAsync(request);
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

    // the balance of the account must cover the new request. hours of PENDING requests on the same account are
    // reserved too, otherwise several requests could together spend more than the balance before any is approved.
    // holiday debt: an account that is still being earned (weeks 1-5, active) may go below 0 by at most
    // max_holiday_debt_days (1 week). week 6 and accounts that finished earning get nothing more, so no debt there
    private async Task check_balance(HolidayAccount account, decimal hours, List<Schedule> schedules, DateOnly today)
    {
        decimal reserved = await db.HolidayRequest
            .Where(r => r.account_id == account.id && r.RequestStatus == RequestStatus.Pending)
            .SumAsync(r => (decimal?)r.Hours) ?? 0;

        decimal available = account.get_earned() - account.get_used() - reserved;

        decimal allowed_debt = 0;
        if (account.holiday_type == HolidayType.FerieEfterFerieloven && account.active)
        {
            Schedule? schedule = Schedule.find_for_date(schedules, today);
            if (schedule != null)
            {
                // one week = max_holiday_debt_days holiday days of the employee's own schedule (full time 5 * 7.40 = 37 h)
                RuleSet rule_set = HolidayParameters.get_rule_set(today);
                allowed_debt = Math.Round(rule_set.max_holiday_debt_days * schedule.get_average_day_hours(), 2,
                    MidpointRounding.AwayFromZero);
            }
        }

        if (available - hours < -allowed_debt)
        {
            string debt_text = allowed_debt > 0 ? $" + {allowed_debt:0.00} h of allowed holiday debt" : "";
            string reserved_text = reserved > 0 ? $" ({reserved:0.00} h are reserved by your pending requests)" : "";
            throw new InvalidOperationException(
                $"Not enough holiday: the request costs {hours:0.00} h, you have {available:0.00} h{debt_text}{reserved_text}");
        }
    }

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
                    HolidayType = x.law != null ? x.law.holiday_type : agreed.holiday_type,
                    Status = x.r.RequestStatus
                }
            ).ToListAsync();
    }

    public async Task DeclineRequest(Guid id) {
        await db.HolidayRequest.Where(r => r.id == id).Set(r => r.RequestStatus, RequestStatus.Rejected).UpdateAsync();
    }

    public async Task AcceptRequest(Guid id) {
        await using var transaction = await db.BeginTransactionAsync();
        var request = await db.HolidayRequest.FirstOrDefaultAsync(r => r.id == id && r.RequestStatus == RequestStatus.Pending);
        
        if (request == null) return;
        
        await db.HolidayRequest.Where(r => r.id == id).Set(r => r.RequestStatus, RequestStatus.Accepted).UpdateAsync();

        var updateLaw = await db.law_holidays.Where(l => l.id == request.account_id)
            .Set(l => l.used_holiday_hours, l => l.used_holiday_hours + request.Hours).UpdateAsync();

        if (updateLaw == 0) {
            await db.agreed_holidays.Where(a => a.id == request.account_id)
                .Set(r => r.used_holiday_hours, r => r.used_holiday_hours + request.Hours).UpdateAsync();
        }
        
        await transaction.CommitAsync();
    }

    // public async Task<RequestDTO[]> GetOngoingRequests(Guid userId)
    // {
    //     var pendingRequests = db.HolidayRequest
    //         .Where(request => request.user_id == userId
    //                           && request.RequestStatus == RequestStatus.Pending);
    //
    //     var lawRequests = await pendingRequests
    //         .Join(db.law_holidays,
    //             request => request.account_id,
    //             account => account.id,
    //             (request, account) => new RequestDTO
    //             {
    //                 user_id = request.user_id,
    //                 StartDate = request.StartDate,
    //                 EndDate = request.EndDate,
    //                 Hours = request.Hours,
    //                 created_at = request.created_at,
    //                 HolidayKind = "law",
    //                 RequestStatus = request.RequestStatus
    //             })
    //         .ToArrayAsync();
    //
    //     var agreedRequests = await pendingRequests
    //         .Join(db.agreed_holidays,
    //             request => request.account_id,
    //             account => account.id,
    //             (request, account) => new RequestDTO
    //             {
    //                 user_id = request.user_id,
    //                 StartDate = request.StartDate,
    //                 EndDate = request.EndDate,
    //                 Hours = request.Hours,
    //                 created_at = request.created_at,
    //                 HolidayKind = "agreed",
    //                 RequestStatus = request.RequestStatus
    //             })
    //         .ToArrayAsync();
    //
    //     return lawRequests.Concat(agreedRequests)
    //         .OrderByDescending(request => request.created_at)
    //         .ToArray();
    // }
}

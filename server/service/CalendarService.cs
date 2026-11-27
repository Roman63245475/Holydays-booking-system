using be;
using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

// the holiday calendar: everybody may see when their colleagues are on holiday (clarification), so the planning
// can take into account how many people are away on a day
public class CalendarService
{
    private readonly Database db;
    private readonly CurrentDateService currentDateService;

    public CalendarService(Database db, CurrentDateService currentDateService)
    {
        this.db = db;
        this.currentDateService = currentDateService;
    }

    // approved holidays of all employees (role User) that overlap the month. pending requests are shown only where
    // they help without exposing unapproved plans: the viewer's own ones, and all of them for the accountant, who
    // decides them and needs to see who is already away
    public async Task<CalendarMonthView> get_month(Guid viewer_id, Role viewer_role, int year, int month)
    {
        DateOnly first = new DateOnly(year, month, 1);
        DateOnly last = HolidayParameters.last_day_of_month(year, month);
        // dates are "yyyy-MM-dd", so comparing the text compares the dates
        string first_text = first.ToString("yyyy-MM-dd");
        string last_text = last.ToString("yyyy-MM-dd");
        bool all_pending = viewer_role == Role.Accountant;

        var rows = await (
                from r in db.HolidayRequest
                join u in db.users on r.user_id equals u.id
                where u.role == Role.User
                      && (r.RequestStatus == RequestStatus.Accepted
                          || (r.RequestStatus == RequestStatus.Pending && (all_pending || r.user_id == viewer_id)))
                select new { r.id, r.user_id, u.name, r.StartDate, r.EndDate, r.RequestStatus, r.account_id })
            .ToListAsync();
        rows = rows
            .Where(r => string.CompareOrdinal(r.StartDate, last_text) <= 0 && string.CompareOrdinal(r.EndDate, first_text) >= 0)
            .ToList();

        List<Guid> account_ids = rows.Select(r => r.account_id).Distinct().ToList();
        HashSet<Guid> law_accounts = (await db.law_holidays
                .Where(l => account_ids.Contains(l.id))
                .Select(l => l.id)
                .ToListAsync())
            .ToHashSet();

        List<CalendarDayOffView> days_off = await db.days_off
            .Where(d => d.date >= first && d.date <= last)
            .OrderBy(d => d.date)
            .Select(d => new CalendarDayOffView { date = d.date, name = d.name, type = d.holiday_type })
            .ToListAsync();

        return new CalendarMonthView
        {
            year = year,
            month = month,
            today = await currentDateService.get_current_date(),
            employee_count = await db.users.CountAsync(u => u.role == Role.User),
            days_off = days_off,
            holidays = rows
                .Select(r => new CalendarHolidayView
                {
                    request_id = r.id,
                    user_id = r.user_id,
                    user_name = r.name,
                    start_date = r.StartDate,
                    end_date = r.EndDate,
                    holiday_type = law_accounts.Contains(r.account_id) ? HolidayType.FerieEfterFerieloven : HolidayType.FerieUge,
                    status = r.RequestStatus == RequestStatus.Accepted ? "approved" : "pending",
                    is_mine = r.user_id == viewer_id
                })
                .OrderBy(h => h.user_name)
                .ThenBy(h => h.start_date)
                .ToList()
        };
    }
}

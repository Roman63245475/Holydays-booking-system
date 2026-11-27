namespace be;

// what a holiday request would be, before it is sent (the employee's request form). nothing is saved
public class RequestPreview
{
    // what the days cost with the employee's schedule (weekends, public holidays and days off cost nothing)
    public decimal hours { get; set; }
    // the same in holiday days (hours / one holiday day of the schedule)
    public decimal holiday_days { get; set; }
    public bool is_main_holiday { get; set; }
    // weeks 1-5 holiday that is not the main holiday and shorter than RequestValidator.RECOMMENDED_BLOCK_DAYS:
    // allowed, but the law recommends blocks of at least 5 days
    public bool short_block { get; set; }
    // the rule the request breaks, null if it can be sent
    public string? error { get; set; }
}

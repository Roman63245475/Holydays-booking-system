using be;
using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

public class RulesetService
{
    private Database db;
    private CurrentDateService currentDateService;

    public RulesetService(Database db, CurrentDateService currentDateService)
    {
        this.db = db;
        this.currentDateService = currentDateService;
    }

    // saves a new rule set. old rule sets are never changed or deleted (full history).
    // throws ArgumentException with a message for the admin if the rule set can not be saved
    public async Task save_rule_set(RuleSet rule_set)
    {
        bool date_taken = await db.rule_sets.AnyAsync(r => r.effective_from == rule_set.effective_from);
        if (date_taken)
        {
            throw new ArgumentException($"A rule set starting on {rule_set.effective_from:yyyy-MM-dd} already exists");
        }

        // a new rule set must not change months that were already accrued.
        // the last history of every account is the month that will be accrued next, all months before it are accrued,
        // so the newest period_start of all histories is the earliest month the new rules may start in
        DateOnly earliest_allowed = await get_earliest_allowed_effective_from();
        if (rule_set.effective_from < earliest_allowed)
        {
            throw new ArgumentException(
                $"The rules can not start before {earliest_allowed:yyyy-MM-dd}: holidays are already accrued for the months before");
        }

        // which preset the rules still are, decided here and not by the client
        rule_set.preset = RuleSetPresets.match(rule_set);
        await db.InsertAsync(rule_set);

        // calculations read the rules from memory, so the new rule set must be loaded there too
        HolidayParameters.load(await db.rule_sets.ToListAsync());
    }

    // the rules that apply "now". now = the month that will be accrued next (the time simulator can be ahead of the
    // real date), or today if nothing has been accrued yet. the rule set itself comes from memory (HolidayParameters)
    public async Task<RuleSet> get_current_rule_set()
    {
        DateOnly now = await get_earliest_allowed_effective_from();
        if (now == DateOnly.MinValue)
        {
            now = DateOnly.FromDateTime(DateTime.Now);
        }
        return HolidayParameters.get_rule_set(now);
    }

    private async Task<DateOnly> get_earliest_allowed_effective_from()
    {
        DateOnly? newest = await currentDateService.get_simulated_date();
        if (newest == null)
        {
            // nothing accrued yet, any date is allowed
            return DateOnly.MinValue;
        }

        // the rules always start on the 1st of a month
        return new DateOnly(newest.Value.Year, newest.Value.Month, 1);
    }
}

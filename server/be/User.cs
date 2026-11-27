namespace be;

public class User
{
    public Guid id { get; set; }
    public string name { get; set; }
    public string email { get; set; }
    public string password { get; set; }
    
    public DateOnly start_date { get; set; }
    public Role role { get; set; }

    // the last working day when the employee leaves the company (fratræden), null while employed.
    // holiday is earned for the exact days up to and including it, then the accounts stop earning and what is
    // left is settled with the final salary by payroll. after it the user can not log in anymore
    public DateOnly? end_date { get; set; }

    private LawHolidays lawHolidays;

    private AgreedHolidays agreed_holidays;

    public User()
    {

    }

    public User(string name, string email, string password, Role role, DateOnly start_date)
    {
        this.id = id;
        this.name = name;
        this.email = email;
        this.password = password;
        this.role = role;
        this.start_date = start_date;
    }
    
    public void set_law_golidays(LawHolidays law_holidays)
    {
        this.lawHolidays = law_holidays;
    }

    public void set_agreed_holidays(AgreedHolidays agreed_holidays)
    {
        this.agreed_holidays = agreed_holidays;
    }

    public LawHolidays get_law_holidays()
    {
        return this.lawHolidays;
    }

    public AgreedHolidays get_agreed_holidays()
    {
        return this.agreed_holidays;
    }
}
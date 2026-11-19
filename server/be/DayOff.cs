namespace be;

public class DayOff
{
    public Guid id { get; set; }
    public string name { get; set; }
    public DateOnly date { get; set; }
    public TypeOfHolidayDay holiday_type { get; set; }

    public DayOff()
    {
        
    }

    public DayOff(Guid id, string name, DateOnly date, TypeOfHolidayDay holiday_type)
    {
        this.id = id;
        this.name = name;
        this.date = date;
        this.holiday_type = holiday_type;
    }
}
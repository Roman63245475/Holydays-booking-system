namespace be;

// one holiday account of one employee in the accountant's balance overview
public class EmployeeBalanceRow
{
    public Guid user_id { get; set; }
    public string name { get; set; } = "";
    // the last working day if the employee leaves / has left
    public DateOnly? end_date { get; set; }
    public HolidayAccountView account { get; set; } = new HolidayAccountView();
}

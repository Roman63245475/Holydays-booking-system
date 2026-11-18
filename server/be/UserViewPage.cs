namespace be;

public class UserViewPage
{
    public string name  { get; set; }
    public string email { get; set; }
    public decimal agreed_earned_holiday_hours { get; set; }
    public decimal agreed_used_holiday_hours { get; set; }

    public decimal available_holiday_hours { get; set; }
    
    public decimal law_earned_holiday_day { get; set; }
    public decimal law_used_holiday_day { get; set; }
    public decimal available_holiday_days { get; set; }
}
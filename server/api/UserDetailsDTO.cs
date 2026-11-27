using be;

namespace api;

// everything the system owner sees on the page of one user: their data, the schedule that applies now and
// their holiday accounts (the same view the employee sees on their own home page)
public class UserDetailsDTO
{
    public Guid id { get; set; }
    public string name { get; set; } = "";
    public string email { get; set; } = "";
    public Role role { get; set; }
    public DateOnly start_date { get; set; }
    public decimal weekly_hours { get; set; }
    public ScheduleDTO? schedule { get; set; }
    public UserViewPage page { get; set; } = new UserViewPage();
}

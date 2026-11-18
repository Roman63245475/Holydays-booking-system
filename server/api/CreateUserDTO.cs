using be;

namespace api;

public class CreateUserDTO
{
    public string name { get; set; }
    public string email { get; set; }
    public string password { get; set; }
    public Role role { get; set; }
    // working hours per weekday, required: without it holiday accrual and the cost of a holiday day can not be calculated
    public ScheduleDTO schedule { get; set; }

    public CreateUserDTO()
    {
    }
}
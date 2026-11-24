using be;

namespace api;

// sent by the admin's edit form (the same form as for creating a user).
// password is optional: empty / null keeps the old password
public class UpdateUserDTO
{
    public string name { get; set; }
    public string email { get; set; }
    public string? password { get; set; }
    public Role role { get; set; }
    // the working schedule from the form. if it differs from the current one, a new schedule period is started
    public ScheduleDTO schedule { get; set; }

    public UpdateUserDTO()
    {
    }

    // returns null if the values are valid, otherwise the error message
    public string? validate()
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "name is required";
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            return "email is required";
        }
        if (!Enum.IsDefined(typeof(Role), role))
        {
            return "role is not valid";
        }
        if (schedule == null)
        {
            return "schedule is required";
        }
        return schedule.validate();
    }
}

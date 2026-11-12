using be;

namespace api;

public class CreateUserDTO
{
    public string name { get; set; }
    public string email { get; set; }
    public string password { get; set; }
    public Role role { get; set; }
    public decimal salary { get; set; }

    public CreateUserDTO()
    {
    }
}
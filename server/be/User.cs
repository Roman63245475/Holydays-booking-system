namespace be;

public class User
{
    public Guid id { get; set; }
    public string name { get; set; }
    public string email { get; set; }
    public string password { get; set; }
    
    public Role role { get; set; }

    public User()
    {

    }

    public User(Guid id,string name, string email, string password, Role role)
    {
        this.id = id;
        this.name = name;
        this.email = email;
        this.password = password;
        this.role = role;
    }
    
    
}

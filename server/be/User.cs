namespace be;

public class User
{
    public Guid id { get; set; }
    public string name { get; set; }
    public string email { get; set; }
    public string password { get; set; }
    
    public decimal salary { get; set; }
    
    public DateOnly start_date { get; set; }
    public Role role { get; set; }

    public User()
    {

    }

    public User(string name, string email, string password, Role role, decimal salary, DateOnly start_date)
    {
        this.id = id;
        this.name = name;
        this.email = email;
        this.password = password;
        this.role = role;
        this.salary = salary;
        this.start_date = start_date;
    }
    
    
}

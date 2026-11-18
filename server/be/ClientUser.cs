namespace be;

public class ClientUser
{
    public Guid id  { get; set; }
    public string name { get; set; }
    public string email { get; set; }
    
    public DateOnly start_date { get; set; }
    public Role role { get; set; }
    
    public ClientUser (Guid id, string username, string email, Role role, DateOnly start_date)
    {
        this.id = id;   
        this.name = username;
        this.email = email;
        this.role = role;
        this.start_date = start_date;
    }

    public ClientUser()
    {
        
    }
}
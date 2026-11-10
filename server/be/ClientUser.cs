namespace be;

public class ClientUser
{
    public string name { get; set; }
    public string email { get; set; }
    public Role role { get; set; }
    
    public ClientUser (string username, string email, Role role)
    {
        this.name = username;
        this.email = email;
        this.role = role;
    }
}
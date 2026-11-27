using be;
using Microsoft.AspNetCore.Mvc;
using service;

namespace api;

public class LoginController: ControllerBase
{

    private LoginService service;

    public LoginController(LoginService service)
    {
        this.service = service;
    }

    [HttpPost(nameof(login))]
    public async Task<ActionResult<ClientUser>> login([FromBody] LoginUserDTO userDTO)
    {
        User user = new User { email = userDTO.email, password = userDTO.Password };
        User login_user;
        try
        {
            login_user = await service.login(user);
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException e)
        {
            // wrong email / password, or the employee has left the company
            return Unauthorized(e.Message);
        }
        HttpContext.Session.SetString("userId", login_user.id.ToString());
        HttpContext.Session.SetString("role", login_user.role.ToString());
        return Ok(new ClientUser{id = login_user.id, name = login_user.name, email = login_user.email, role = login_user.role});
    }

    [HttpPost("logout")]
    public IActionResult Logout() {
        HttpContext.Session.Clear();
        return Ok();
    }
}
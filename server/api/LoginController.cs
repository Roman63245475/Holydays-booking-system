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
        User login_user = await service.login(user);
        HttpContext.Session.SetString("userId", login_user.id.ToString());
        HttpContext.Session.SetString("role", login_user.role.ToString());
        return Ok(new ClientUser(login_user.name, login_user.email, login_user.role));
    }
}
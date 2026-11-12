using be;
using Microsoft.AspNetCore.Mvc;
using service;

namespace api;

public class UserController: ControllerBase
{
    
    private UserService userService;

    public UserController(UserService userService)
    {
        this.userService = userService;
    }

    [HttpPost(nameof(create))]
    public async Task<IActionResult> create([FromBody] CreateUserDTO userDTO)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        User user = new User(userDTO.name, userDTO.email, userDTO.password, userDTO.role, userDTO.salary, today);
        try
        {
            await userService.create_user(user);
            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }
}
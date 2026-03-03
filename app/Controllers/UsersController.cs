using Microsoft.AspNetCore.Mvc;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Controllers;

/// <summary>Users API</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly DatabaseService _db;

    public UsersController(DatabaseService db) => _db = db;

    /// <summary>Get all users</summary>
    [HttpGet]
    public async Task<ActionResult<List<User>>> GetUsers([FromQuery] bool includeInactive = false)
    {
        var users = await _db.GetUsersAsync(includeInactive);
        return Ok(users);
    }

    /// <summary>Get a user by ID</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(int id)
    {
        var user = await _db.GetUserByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(user);
    }

    /// <summary>Create a new user</summary>
    [HttpPost]
    public async Task<ActionResult<int>> CreateUser([FromBody] CreateUserRequest req)
    {
        var id = await _db.CreateUserAsync(req.UserName, req.Email, req.RoleId, req.ManagerId);
        if (id < 0) return StatusCode(500, _db.LastError?.Message ?? "Error creating user");
        return CreatedAtAction(nameof(GetUser), new { id }, id);
    }

    /// <summary>Update a user</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest req)
    {
        await _db.UpdateUserAsync(id, req.UserName, req.Email, req.RoleId, req.ManagerId, req.IsActive);
        if (_db.LastError != null) return StatusCode(500, _db.LastError.Message);
        return NoContent();
    }

    /// <summary>Deactivate a user</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        await _db.DeleteUserAsync(id);
        if (_db.LastError != null) return StatusCode(500, _db.LastError.Message);
        return NoContent();
    }

    /// <summary>Get all roles</summary>
    [HttpGet("roles")]
    public async Task<ActionResult<List<Role>>> GetRoles()
    {
        var roles = await _db.GetRolesAsync();
        return Ok(roles);
    }
}

public record CreateUserRequest(string UserName, string Email, int RoleId, int? ManagerId);
public record UpdateUserRequest(string UserName, string Email, int RoleId, int? ManagerId, bool IsActive);

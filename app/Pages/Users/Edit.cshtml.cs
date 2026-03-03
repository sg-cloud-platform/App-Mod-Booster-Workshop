using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Users;

public class EditModel : PageModel
{
    private readonly DatabaseService _db;

    public EditModel(DatabaseService db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> RoleOptions { get; set; } = new();
    public List<SelectListItem> ManagerOptions { get; set; } = new();
    public AppError? Error { get; set; }

    public class InputModel
    {
        public int UserId { get; set; }
        [Required] public string UserName { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public int RoleId { get; set; }
        public int? ManagerId { get; set; }
        public bool IsActive { get; set; }
    }

    public async Task OnGetAsync(int id)
    {
        var user = await _db.GetUserByIdAsync(id);
        if (user != null)
        {
            Input = new InputModel
            {
                UserId = user.UserId,
                UserName = user.UserName,
                Email = user.Email,
                RoleId = user.RoleId,
                ManagerId = user.ManagerId,
                IsActive = user.IsActive
            };
        }
        await LoadOptions();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await LoadOptions(); return Page(); }

        await _db.UpdateUserAsync(Input.UserId, Input.UserName, Input.Email, Input.RoleId, Input.ManagerId, Input.IsActive);
        if (_db.LastError != null) { Error = _db.LastError; await LoadOptions(); return Page(); }

        return RedirectToPage("Index");
    }

    private async Task LoadOptions()
    {
        var roles = await _db.GetRolesAsync();
        RoleOptions = roles.Select(r => new SelectListItem(r.RoleName, r.RoleId.ToString())).ToList();

        var users = await _db.GetUsersAsync();
        ManagerOptions = users.Select(u => new SelectListItem(u.UserName, u.UserId.ToString())).ToList();
    }
}

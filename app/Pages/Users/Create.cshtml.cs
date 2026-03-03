using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Users;

public class CreateModel : PageModel
{
    private readonly DatabaseService _db;

    public CreateModel(DatabaseService db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> RoleOptions { get; set; } = new();
    public List<SelectListItem> ManagerOptions { get; set; } = new();
    public AppError? Error { get; set; }

    public class InputModel
    {
        [Required] public string UserName { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public int RoleId { get; set; }
        public int? ManagerId { get; set; }
    }

    public async Task OnGetAsync() => await LoadOptions();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await LoadOptions(); return Page(); }

        var id = await _db.CreateUserAsync(Input.UserName, Input.Email, Input.RoleId, Input.ManagerId);
        if (id < 0) { Error = _db.LastError; await LoadOptions(); return Page(); }

        return RedirectToPage("Index");
    }

    private async Task LoadOptions()
    {
        var roles = await _db.GetRolesAsync();
        RoleOptions = roles.Select(r => new SelectListItem(r.RoleName, r.RoleId.ToString())).ToList();

        var managers = await _db.GetUsersAsync();
        ManagerOptions = managers.Select(u => new SelectListItem(u.UserName, u.UserId.ToString())).ToList();
    }
}

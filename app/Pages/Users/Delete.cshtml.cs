using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Users;

public class DeleteModel : PageModel
{
    private readonly DatabaseService _db;

    public DeleteModel(DatabaseService db) => _db = db;

    public string? UserName { get; set; }

    public async Task OnGetAsync(int id)
    {
        var user = await _db.GetUserByIdAsync(id);
        UserName = user?.UserName;
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        await _db.DeleteUserAsync(id);
        return RedirectToPage("Index");
    }
}

using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Users;

public class IndexModel : PageModel
{
    private readonly DatabaseService _db;

    public IndexModel(DatabaseService db) => _db = db;

    public List<User> Users { get; set; } = new();
    public AppError? Error { get; set; }

    public async Task OnGetAsync()
    {
        Users = await _db.GetUsersAsync(includeInactive: true);
        if (_db.LastError != null) Error = _db.LastError;
    }
}

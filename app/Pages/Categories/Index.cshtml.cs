using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Categories;

public class IndexModel : PageModel
{
    private readonly DatabaseService _db;

    public IndexModel(DatabaseService db) => _db = db;

    public List<Category> Categories { get; set; } = new();
    public AppError? Error { get; set; }

    public async Task OnGetAsync()
    {
        Categories = await _db.GetCategoriesAsync(includeInactive: true);
        if (_db.LastError != null) Error = _db.LastError;
    }
}

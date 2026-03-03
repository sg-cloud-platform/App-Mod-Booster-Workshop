using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Categories;

public class DeleteModel : PageModel
{
    private readonly DatabaseService _db;

    public DeleteModel(DatabaseService db) => _db = db;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        await _db.DeleteCategoryAsync(id);
        return RedirectToPage("Index");
    }
}

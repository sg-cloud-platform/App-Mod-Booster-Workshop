using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Expenses;

public class DeleteModel : PageModel
{
    private readonly DatabaseService _db;

    public DeleteModel(DatabaseService db) => _db = db;

    public Expense? Expense { get; set; }

    public async Task OnGetAsync(int id)
    {
        Expense = await _db.GetExpenseByIdAsync(id);
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        await _db.DeleteExpenseAsync(id);
        return RedirectToPage("Index");
    }
}

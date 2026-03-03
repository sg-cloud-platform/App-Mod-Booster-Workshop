using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Expenses;

public class DetailsModel : PageModel
{
    private readonly DatabaseService _db;

    public DetailsModel(DatabaseService db) => _db = db;

    public Expense? Expense { get; set; }
    public AppError? Error { get; set; }

    public async Task OnGetAsync(int id)
    {
        Expense = await _db.GetExpenseByIdAsync(id);
        if (_db.LastError != null) Error = _db.LastError;
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        await _db.SubmitExpenseAsync(id);
        return RedirectToPage("Index");
    }
}

using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Pages;

public class IndexModel : PageModel
{
    private readonly DatabaseService _db;

    public IndexModel(DatabaseService db) => _db = db;

    public List<ExpenseSummary> Summary { get; set; } = new();
    public List<Expense> RecentExpenses { get; set; } = new();
    public AppError? Error { get; set; }

    public async Task OnGetAsync()
    {
        Summary = await _db.GetExpenseSummaryAsync();
        if (_db.LastError != null) Error = _db.LastError;

        RecentExpenses = await _db.GetExpensesAsync();
        if (_db.LastError != null) Error ??= _db.LastError;

        RecentExpenses = RecentExpenses.Take(10).ToList();
    }
}

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Expenses;

public class IndexModel : PageModel
{
    private readonly DatabaseService _db;

    public IndexModel(DatabaseService db) => _db = db;

    public List<Expense> Expenses { get; set; } = new();
    public List<SelectListItem> StatusOptions { get; set; } = new();
    public List<SelectListItem> UserOptions { get; set; } = new();
    public List<SelectListItem> CategoryOptions { get; set; } = new();
    public AppError? Error { get; set; }

    public async Task OnGetAsync(int? statusId, int? userId, int? categoryId)
    {
        Expenses = await _db.GetExpensesAsync(statusId, userId, categoryId);
        if (_db.LastError != null) Error = _db.LastError;

        var statuses = await _db.GetExpenseStatusesAsync();
        StatusOptions = statuses.Select(s => new SelectListItem(s.StatusName, s.StatusId.ToString(), s.StatusId == statusId)).ToList();

        var users = await _db.GetUsersAsync();
        UserOptions = users.Select(u => new SelectListItem(u.UserName, u.UserId.ToString(), u.UserId == userId)).ToList();

        var cats = await _db.GetCategoriesAsync();
        CategoryOptions = cats.Select(c => new SelectListItem(c.CategoryName, c.CategoryId.ToString(), c.CategoryId == categoryId)).ToList();
    }
}

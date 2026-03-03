using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Expenses;

public class ReviewModel : PageModel
{
    private readonly DatabaseService _db;

    public ReviewModel(DatabaseService db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Expense? Expense { get; set; }
    public List<SelectListItem> ManagerOptions { get; set; } = new();
    public AppError? Error { get; set; }

    public class InputModel
    {
        public int ExpenseId { get; set; }
        public int ReviewedBy { get; set; }
    }

    public async Task OnGetAsync(int id)
    {
        Expense = await _db.GetExpenseByIdAsync(id);
        Input.ExpenseId = id;
        if (_db.LastError != null) Error = _db.LastError;
        await LoadManagers();
    }

    public async Task<IActionResult> OnPostAsync(string action)
    {
        // Approved = 3, Rejected = 4
        int statusId = action == "approve" ? 3 : 4;
        await _db.UpdateExpenseStatusAsync(Input.ExpenseId, statusId, Input.ReviewedBy);
        if (_db.LastError != null)
        {
            Error = _db.LastError;
            Expense = await _db.GetExpenseByIdAsync(Input.ExpenseId);
            await LoadManagers();
            return Page();
        }
        return RedirectToPage("Index");
    }

    private async Task LoadManagers()
    {
        var users = await _db.GetUsersAsync();
        ManagerOptions = users
            .Where(u => u.RoleName == "Manager")
            .Select(u => new SelectListItem(u.UserName, u.UserId.ToString()))
            .ToList();
        // fallback: show all users if no managers exist
        if (!ManagerOptions.Any())
            ManagerOptions = users.Select(u => new SelectListItem(u.UserName, u.UserId.ToString())).ToList();
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Expenses;

public class CreateModel : PageModel
{
    private readonly DatabaseService _db;

    public CreateModel(DatabaseService db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> UserOptions { get; set; } = new();
    public List<SelectListItem> CategoryOptions { get; set; } = new();
    public List<SelectListItem> StatusOptions { get; set; } = new();
    public AppError? Error { get; set; }

    public class InputModel
    {
        [Required] public int UserId { get; set; }
        [Required] public int CategoryId { get; set; }
        [Required, Range(0.01, 1000000)] public decimal AmountGBP { get; set; }
        [Required] public DateTime ExpenseDate { get; set; } = DateTime.Today;
        public string? Description { get; set; }
        public string? ReceiptFile { get; set; }
        public int StatusId { get; set; } = 1;
    }

    public async Task OnGetAsync()
    {
        await LoadOptions();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadOptions();
            return Page();
        }

        var amountMinor = (int)(Input.AmountGBP * 100);
        await _db.CreateExpenseAsync(Input.UserId, Input.CategoryId, amountMinor, "GBP",
            Input.ExpenseDate, Input.Description, Input.ReceiptFile, Input.StatusId);

        if (_db.LastError != null)
        {
            Error = _db.LastError;
            await LoadOptions();
            return Page();
        }

        return RedirectToPage("Index");
    }

    private async Task LoadOptions()
    {
        var users = await _db.GetUsersAsync();
        UserOptions = users.Select(u => new SelectListItem(u.UserName, u.UserId.ToString())).ToList();

        var cats = await _db.GetCategoriesAsync();
        CategoryOptions = cats.Select(c => new SelectListItem(c.CategoryName, c.CategoryId.ToString())).ToList();

        var statuses = await _db.GetExpenseStatusesAsync();
        StatusOptions = statuses.Select(s => new SelectListItem(s.StatusName, s.StatusId.ToString())).ToList();
    }
}

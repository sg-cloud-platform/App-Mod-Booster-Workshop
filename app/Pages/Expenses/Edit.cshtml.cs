using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Expenses;

public class EditModel : PageModel
{
    private readonly DatabaseService _db;

    public EditModel(DatabaseService db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> CategoryOptions { get; set; } = new();
    public AppError? Error { get; set; }

    public class InputModel
    {
        public int ExpenseId { get; set; }
        [Required] public int CategoryId { get; set; }
        [Required, Range(0.01, 1000000)] public decimal AmountGBP { get; set; }
        [Required] public DateTime ExpenseDate { get; set; }
        public string? Description { get; set; }
        public string? ReceiptFile { get; set; }
    }

    public async Task OnGetAsync(int id)
    {
        var expense = await _db.GetExpenseByIdAsync(id);
        if (expense != null)
        {
            Input = new InputModel
            {
                ExpenseId = expense.ExpenseId,
                CategoryId = expense.CategoryId,
                AmountGBP = expense.AmountGBP,
                ExpenseDate = expense.ExpenseDate,
                Description = expense.Description,
                ReceiptFile = expense.ReceiptFile
            };
        }
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
        await _db.UpdateExpenseAsync(Input.ExpenseId, Input.CategoryId, amountMinor, "GBP",
            Input.ExpenseDate, Input.Description, Input.ReceiptFile);

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
        var cats = await _db.GetCategoriesAsync();
        CategoryOptions = cats.Select(c => new SelectListItem(c.CategoryName, c.CategoryId.ToString())).ToList();
    }
}

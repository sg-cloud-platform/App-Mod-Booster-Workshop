using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Categories;

public class EditModel : PageModel
{
    private readonly DatabaseService _db;

    public EditModel(DatabaseService db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public AppError? Error { get; set; }

    public class InputModel
    {
        public int CategoryId { get; set; }
        [Required] public string CategoryName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public async Task OnGetAsync(int id)
    {
        var cat = await _db.GetCategoriesAsync(true);
        var found = cat.FirstOrDefault(c => c.CategoryId == id);
        if (found != null)
        {
            Input = new InputModel { CategoryId = found.CategoryId, CategoryName = found.CategoryName, IsActive = found.IsActive };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        await _db.UpdateCategoryAsync(Input.CategoryId, Input.CategoryName, Input.IsActive);
        if (_db.LastError != null) { Error = _db.LastError; return Page(); }
        return RedirectToPage("Index");
    }
}

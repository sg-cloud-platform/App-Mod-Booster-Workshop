using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Categories;

public class CreateModel : PageModel
{
    private readonly DatabaseService _db;

    public CreateModel(DatabaseService db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public AppError? Error { get; set; }

    public class InputModel
    {
        [Required] public string CategoryName { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var id = await _db.CreateCategoryAsync(Input.CategoryName);
        if (id < 0) { Error = _db.LastError; return Page(); }

        return RedirectToPage("Index");
    }
}

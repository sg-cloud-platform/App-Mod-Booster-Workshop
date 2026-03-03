using Microsoft.AspNetCore.Mvc;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Controllers;

/// <summary>Categories API</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly DatabaseService _db;

    public CategoriesController(DatabaseService db) => _db = db;

    /// <summary>Get all categories</summary>
    [HttpGet]
    public async Task<ActionResult<List<Category>>> GetCategories([FromQuery] bool includeInactive = false)
    {
        var categories = await _db.GetCategoriesAsync(includeInactive);
        return Ok(categories);
    }

    /// <summary>Create a new category</summary>
    [HttpPost]
    public async Task<ActionResult<int>> CreateCategory([FromBody] CreateCategoryRequest req)
    {
        var id = await _db.CreateCategoryAsync(req.CategoryName);
        if (id < 0) return StatusCode(500, _db.LastError?.Message ?? "Error creating category");
        return CreatedAtAction(nameof(GetCategories), new { id }, id);
    }

    /// <summary>Update a category</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest req)
    {
        await _db.UpdateCategoryAsync(id, req.CategoryName, req.IsActive);
        if (_db.LastError != null) return StatusCode(500, _db.LastError.Message);
        return NoContent();
    }

    /// <summary>Deactivate a category</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        await _db.DeleteCategoryAsync(id);
        if (_db.LastError != null) return StatusCode(500, _db.LastError.Message);
        return NoContent();
    }

    /// <summary>Get all expense statuses</summary>
    [HttpGet("statuses")]
    public async Task<ActionResult<List<ExpenseStatus>>> GetStatuses()
    {
        var statuses = await _db.GetExpenseStatusesAsync();
        return Ok(statuses);
    }
}

public record CreateCategoryRequest(string CategoryName);
public record UpdateCategoryRequest(string CategoryName, bool IsActive);

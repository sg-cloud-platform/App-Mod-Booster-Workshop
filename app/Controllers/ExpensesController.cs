using Microsoft.AspNetCore.Mvc;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Controllers;

/// <summary>Expenses API</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly DatabaseService _db;

    public ExpensesController(DatabaseService db) => _db = db;

    /// <summary>Get all expenses with optional filters</summary>
    [HttpGet]
    public async Task<ActionResult<List<Expense>>> GetExpenses(
        [FromQuery] int? statusId = null,
        [FromQuery] int? userId = null,
        [FromQuery] int? categoryId = null)
    {
        var expenses = await _db.GetExpensesAsync(statusId, userId, categoryId);
        return Ok(expenses);
    }

    /// <summary>Get a single expense by ID</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Expense>> GetExpense(int id)
    {
        var expense = await _db.GetExpenseByIdAsync(id);
        if (expense == null) return NotFound();
        return Ok(expense);
    }

    /// <summary>Create a new expense</summary>
    [HttpPost]
    public async Task<ActionResult<int>> CreateExpense([FromBody] CreateExpenseRequest req)
    {
        var id = await _db.CreateExpenseAsync(
            req.UserId, req.CategoryId, req.AmountMinor, req.Currency ?? "GBP",
            req.ExpenseDate, req.Description, req.ReceiptFile, req.StatusId);
        if (id < 0) return StatusCode(500, _db.LastError?.Message ?? "Error creating expense");
        return CreatedAtAction(nameof(GetExpense), new { id }, id);
    }

    /// <summary>Update an expense</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] UpdateExpenseRequest req)
    {
        await _db.UpdateExpenseAsync(id, req.CategoryId, req.AmountMinor, req.Currency ?? "GBP",
            req.ExpenseDate, req.Description, req.ReceiptFile);
        if (_db.LastError != null) return StatusCode(500, _db.LastError.Message);
        return NoContent();
    }

    /// <summary>Submit an expense for review</summary>
    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitExpense(int id)
    {
        await _db.SubmitExpenseAsync(id);
        if (_db.LastError != null) return StatusCode(500, _db.LastError.Message);
        return NoContent();
    }

    /// <summary>Approve or reject an expense</summary>
    [HttpPost("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest req)
    {
        await _db.UpdateExpenseStatusAsync(id, req.StatusId, req.ReviewedBy);
        if (_db.LastError != null) return StatusCode(500, _db.LastError.Message);
        return NoContent();
    }

    /// <summary>Delete an expense</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        await _db.DeleteExpenseAsync(id);
        if (_db.LastError != null) return StatusCode(500, _db.LastError.Message);
        return NoContent();
    }

    /// <summary>Get expense summary by status</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<ExpenseSummary>>> GetSummary()
    {
        var summary = await _db.GetExpenseSummaryAsync();
        return Ok(summary);
    }
}

public record CreateExpenseRequest(
    int UserId, int CategoryId, int AmountMinor, string? Currency,
    DateTime ExpenseDate, string? Description, string? ReceiptFile, int StatusId = 1);

public record UpdateExpenseRequest(
    int CategoryId, int AmountMinor, string? Currency,
    DateTime ExpenseDate, string? Description, string? ReceiptFile);

public record UpdateStatusRequest(int StatusId, int ReviewedBy);

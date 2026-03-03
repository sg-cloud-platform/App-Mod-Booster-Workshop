using Microsoft.Data.SqlClient;
using ExpenseApp.Models;
using System.Data;
using System.Runtime.CompilerServices;

namespace ExpenseApp.Services;

public class AppError
{
    public string Message { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public bool IsManagedIdentityError { get; set; }
    public string? ManagedIdentityFix { get; set; }
}

public class DatabaseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseService> _logger;
    public AppError? LastError { get; private set; }

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private SqlConnection GetConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");
        return new SqlConnection(connectionString);
    }

    private void SetError(Exception ex, string fileName, int lineNumber)
    {
        bool isMI = ex.Message.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase)
                 || ex.Message.Contains("Active Directory", StringComparison.OrdinalIgnoreCase)
                 || ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase)
                 || ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
                 || ex.Message.Contains("Login failed", StringComparison.OrdinalIgnoreCase);

        string? fix = isMI
            ? "Managed Identity authentication failed. Ensure: 1) The App Service has a user-assigned Managed Identity assigned. " +
              "2) The AZURE_CLIENT_ID app setting is set to the Managed Identity's Client ID. " +
              "3) The Managed Identity has been added as a database user with: CREATE USER [mid-name] FROM EXTERNAL PROVIDER; and granted db_datareader, db_datawriter, and EXECUTE permissions. " +
              "4) For local development, change the connection string to use 'Authentication=Active Directory Default' and run 'az login' first."
            : null;

        LastError = new AppError
        {
            Message = ex.Message,
            FileName = System.IO.Path.GetFileName(fileName),
            LineNumber = lineNumber,
            IsManagedIdentityError = isMI,
            ManagedIdentityFix = fix
        };
        _logger.LogError(ex, "Database error at {File}:{Line}", fileName, lineNumber);
    }

    // ── EXPENSES ─────────────────────────────────────────────────────────────

    public async Task<List<Expense>> GetExpensesAsync(int? statusId = null, int? userId = null, int? categoryId = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenses", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);

            var expenses = new List<Expense>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                expenses.Add(MapExpense(reader));
            return expenses;
        }
        catch (Exception ex)
        {
            SetError(ex, file, line);
            return GetDummyExpenses();
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenseById", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return MapExpense(reader);
            return null;
        }
        catch (Exception ex)
        {
            SetError(ex, file, line);
            return null;
        }
    }

    public async Task<int> CreateExpenseAsync(int userId, int categoryId, int amountMinor, string currency,
        DateTime expenseDate, string? description, string? receiptFile, int statusId = 1,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_CreateExpense", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@CategoryId", categoryId);
            cmd.Parameters.AddWithValue("@AmountMinor", amountMinor);
            cmd.Parameters.AddWithValue("@Currency", currency);
            cmd.Parameters.AddWithValue("@ExpenseDate", expenseDate);
            cmd.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReceiptFile", (object?)receiptFile ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StatusId", statusId);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            SetError(ex, file, line);
            return -1;
        }
    }

    public async Task UpdateExpenseAsync(int expenseId, int categoryId, int amountMinor, string currency,
        DateTime expenseDate, string? description, string? receiptFile,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_UpdateExpense", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            cmd.Parameters.AddWithValue("@CategoryId", categoryId);
            cmd.Parameters.AddWithValue("@AmountMinor", amountMinor);
            cmd.Parameters.AddWithValue("@Currency", currency);
            cmd.Parameters.AddWithValue("@ExpenseDate", expenseDate);
            cmd.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReceiptFile", (object?)receiptFile ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { SetError(ex, file, line); }
    }

    public async Task SubmitExpenseAsync(int expenseId,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_SubmitExpense", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { SetError(ex, file, line); }
    }

    public async Task UpdateExpenseStatusAsync(int expenseId, int statusId, int reviewedBy,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_UpdateExpenseStatus", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            cmd.Parameters.AddWithValue("@StatusId", statusId);
            cmd.Parameters.AddWithValue("@ReviewedBy", reviewedBy);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { SetError(ex, file, line); }
    }

    public async Task DeleteExpenseAsync(int expenseId,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_DeleteExpense", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { SetError(ex, file, line); }
    }

    // ── USERS ─────────────────────────────────────────────────────────────────

    public async Task<List<User>> GetUsersAsync(bool includeInactive = false,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetUsers", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@IncludeInactive", includeInactive ? 1 : 0);
            var users = new List<User>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                users.Add(MapUser(reader));
            return users;
        }
        catch (Exception ex)
        {
            SetError(ex, file, line);
            return GetDummyUsers();
        }
    }

    public async Task<User?> GetUserByIdAsync(int userId,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetUserById", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", userId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return MapUser(reader);
            return null;
        }
        catch (Exception ex)
        {
            SetError(ex, file, line);
            return null;
        }
    }

    public async Task<int> CreateUserAsync(string userName, string email, int roleId, int? managerId,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_CreateUser", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserName", userName);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            cmd.Parameters.AddWithValue("@ManagerId", (object?)managerId ?? DBNull.Value);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            SetError(ex, file, line);
            return -1;
        }
    }

    public async Task UpdateUserAsync(int userId, string userName, string email, int roleId, int? managerId, bool isActive,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_UpdateUser", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@UserName", userName);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            cmd.Parameters.AddWithValue("@ManagerId", (object?)managerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { SetError(ex, file, line); }
    }

    public async Task DeleteUserAsync(int userId,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_DeleteUser", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { SetError(ex, file, line); }
    }

    // ── CATEGORIES ─────────────────────────────────────────────────────────────

    public async Task<List<Category>> GetCategoriesAsync(bool includeInactive = false,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetCategories", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@IncludeInactive", includeInactive ? 1 : 0);
            var categories = new List<Category>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                categories.Add(new Category
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            return categories;
        }
        catch (Exception ex)
        {
            SetError(ex, file, line);
            return GetDummyCategories();
        }
    }

    public async Task<int> CreateCategoryAsync(string categoryName,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_CreateCategory", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@CategoryName", categoryName);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex) { SetError(ex, file, line); return -1; }
    }

    public async Task UpdateCategoryAsync(int categoryId, string categoryName, bool isActive,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_UpdateCategory", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@CategoryId", categoryId);
            cmd.Parameters.AddWithValue("@CategoryName", categoryName);
            cmd.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { SetError(ex, file, line); }
    }

    public async Task DeleteCategoryAsync(int categoryId,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_DeleteCategory", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@CategoryId", categoryId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { SetError(ex, file, line); }
    }

    // ── LOOKUPS ────────────────────────────────────────────────────────────────

    public async Task<List<Role>> GetRolesAsync(
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetRoles", conn) { CommandType = CommandType.StoredProcedure };
            var roles = new List<Role>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                roles.Add(new Role
                {
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"))
                });
            return roles;
        }
        catch (Exception ex)
        {
            SetError(ex, file, line);
            return new List<Role>
            {
                new() { RoleId = 1, RoleName = "Employee" },
                new() { RoleId = 2, RoleName = "Manager" }
            };
        }
    }

    public async Task<List<ExpenseStatus>> GetExpenseStatusesAsync(
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenseStatuses", conn) { CommandType = CommandType.StoredProcedure };
            var statuses = new List<ExpenseStatus>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            return statuses;
        }
        catch (Exception ex)
        {
            SetError(ex, file, line);
            return new List<ExpenseStatus>
            {
                new() { StatusId = 1, StatusName = "Draft" },
                new() { StatusId = 2, StatusName = "Submitted" },
                new() { StatusId = 3, StatusName = "Approved" },
                new() { StatusId = 4, StatusName = "Rejected" }
            };
        }
    }

    public async Task<List<ExpenseSummary>> GetExpenseSummaryAsync(
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        LastError = null;
        try
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenseSummary", conn) { CommandType = CommandType.StoredProcedure };
            var summaries = new List<ExpenseSummary>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                summaries.Add(new ExpenseSummary
                {
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                    ExpenseCount = reader.GetInt32(reader.GetOrdinal("ExpenseCount")),
                    TotalAmountMinor = reader.GetInt32(reader.GetOrdinal("TotalAmountMinor"))
                });
            return summaries;
        }
        catch (Exception ex)
        {
            SetError(ex, file, line);
            return new List<ExpenseSummary>
            {
                new() { StatusName = "Draft", ExpenseCount = 1, TotalAmountMinor = 799 },
                new() { StatusName = "Submitted", ExpenseCount = 1, TotalAmountMinor = 2540 },
                new() { StatusName = "Approved", ExpenseCount = 2, TotalAmountMinor = 13725 },
                new() { StatusName = "Rejected", ExpenseCount = 0, TotalAmountMinor = 0 }
            };
        }
    }

    // ── MAPPERS ────────────────────────────────────────────────────────────────

    private static Expense MapExpense(SqlDataReader r) => new()
    {
        ExpenseId = r.GetInt32(r.GetOrdinal("ExpenseId")),
        UserId = r.GetInt32(r.GetOrdinal("UserId")),
        UserName = r.IsDBNull(r.GetOrdinal("UserName")) ? null : r.GetString(r.GetOrdinal("UserName")),
        CategoryId = r.GetInt32(r.GetOrdinal("CategoryId")),
        CategoryName = r.IsDBNull(r.GetOrdinal("CategoryName")) ? null : r.GetString(r.GetOrdinal("CategoryName")),
        StatusId = r.GetInt32(r.GetOrdinal("StatusId")),
        StatusName = r.IsDBNull(r.GetOrdinal("StatusName")) ? null : r.GetString(r.GetOrdinal("StatusName")),
        AmountMinor = r.GetInt32(r.GetOrdinal("AmountMinor")),
        Currency = r.GetString(r.GetOrdinal("Currency")),
        ExpenseDate = r.GetDateTime(r.GetOrdinal("ExpenseDate")),
        Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
        ReceiptFile = r.IsDBNull(r.GetOrdinal("ReceiptFile")) ? null : r.GetString(r.GetOrdinal("ReceiptFile")),
        SubmittedAt = r.IsDBNull(r.GetOrdinal("SubmittedAt")) ? null : r.GetDateTime(r.GetOrdinal("SubmittedAt")),
        ReviewedBy = r.IsDBNull(r.GetOrdinal("ReviewedBy")) ? null : r.GetInt32(r.GetOrdinal("ReviewedBy")),
        ReviewedByName = r.IsDBNull(r.GetOrdinal("ReviewedByName")) ? null : r.GetString(r.GetOrdinal("ReviewedByName")),
        ReviewedAt = r.IsDBNull(r.GetOrdinal("ReviewedAt")) ? null : r.GetDateTime(r.GetOrdinal("ReviewedAt")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt"))
    };

    private static User MapUser(SqlDataReader r) => new()
    {
        UserId = r.GetInt32(r.GetOrdinal("UserId")),
        UserName = r.GetString(r.GetOrdinal("UserName")),
        Email = r.GetString(r.GetOrdinal("Email")),
        RoleId = r.GetInt32(r.GetOrdinal("RoleId")),
        RoleName = r.IsDBNull(r.GetOrdinal("RoleName")) ? null : r.GetString(r.GetOrdinal("RoleName")),
        ManagerId = r.IsDBNull(r.GetOrdinal("ManagerId")) ? null : r.GetInt32(r.GetOrdinal("ManagerId")),
        ManagerName = r.IsDBNull(r.GetOrdinal("ManagerName")) ? null : r.GetString(r.GetOrdinal("ManagerName")),
        IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt"))
    };

    // ── DUMMY DATA ─────────────────────────────────────────────────────────────

    private static List<Expense> GetDummyExpenses() => new()
    {
        new() { ExpenseId = 1, UserId = 1, UserName = "Alice Example (Demo)", CategoryId = 1, CategoryName = "Travel",
            StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, Currency = "GBP",
            ExpenseDate = DateTime.Today.AddDays(-14), Description = "Taxi from airport [DEMO DATA]",
            SubmittedAt = DateTime.UtcNow.AddDays(-13), CreatedAt = DateTime.UtcNow.AddDays(-14) },
        new() { ExpenseId = 2, UserId = 1, UserName = "Alice Example (Demo)", CategoryId = 2, CategoryName = "Meals",
            StatusId = 3, StatusName = "Approved", AmountMinor = 1425, Currency = "GBP",
            ExpenseDate = DateTime.Today.AddDays(-30), Description = "Client lunch [DEMO DATA]",
            SubmittedAt = DateTime.UtcNow.AddDays(-29), CreatedAt = DateTime.UtcNow.AddDays(-30) },
        new() { ExpenseId = 3, UserId = 1, UserName = "Alice Example (Demo)", CategoryId = 3, CategoryName = "Supplies",
            StatusId = 1, StatusName = "Draft", AmountMinor = 799, Currency = "GBP",
            ExpenseDate = DateTime.Today.AddDays(-5), Description = "Office stationery [DEMO DATA]",
            CreatedAt = DateTime.UtcNow.AddDays(-5) }
    };

    private static List<User> GetDummyUsers() => new()
    {
        new() { UserId = 1, UserName = "Alice Example (Demo)", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
        new() { UserId = 2, UserName = "Bob Manager (Demo)", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) }
    };

    private static List<Category> GetDummyCategories() => new()
    {
        new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
        new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
        new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
        new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
        new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
    };
}

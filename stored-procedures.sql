-- Stored Procedures for Expense Management System
-- All app data access goes through these stored procedures
-- Use "CREATE OR ALTER PROCEDURE" for idempotent deployments

SET NOCOUNT ON;
GO

-- =============================================
-- EXPENSES
-- =============================================

-- Get expenses with optional filters
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenses
    @StatusId   INT = NULL,
    @UserId     INT = NULL,
    @CategoryId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rv.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u          ON e.UserId     = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s  ON e.StatusId   = s.StatusId
    LEFT JOIN dbo.Users rv    ON e.ReviewedBy = rv.UserId
    WHERE (@StatusId   IS NULL OR e.StatusId   = @StatusId)
      AND (@UserId     IS NULL OR e.UserId     = @UserId)
      AND (@CategoryId IS NULL OR e.CategoryId = @CategoryId)
    ORDER BY e.CreatedAt DESC;
END
GO

-- Get single expense by ID
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rv.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u          ON e.UserId     = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s  ON e.StatusId   = s.StatusId
    LEFT JOIN dbo.Users rv    ON e.ReviewedBy = rv.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- Create new expense
CREATE OR ALTER PROCEDURE dbo.usp_CreateExpense
    @UserId      INT,
    @CategoryId  INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL,
    @StatusId    INT = 1   -- default Draft
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Expenses
        (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, CreatedAt)
    VALUES
        (@UserId, @CategoryId, @StatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS ExpenseId;
END
GO

-- Update expense (by owner, only when in Draft)
CREATE OR ALTER PROCEDURE dbo.usp_UpdateExpense
    @ExpenseId   INT,
    @CategoryId  INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Expenses
    SET CategoryId  = @CategoryId,
        AmountMinor = @AmountMinor,
        Currency    = @Currency,
        ExpenseDate = @ExpenseDate,
        Description = @Description,
        ReceiptFile = @ReceiptFile
    WHERE ExpenseId = @ExpenseId;
END
GO

-- Submit expense (moves to Submitted status)
CREATE OR ALTER PROCEDURE dbo.usp_SubmitExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Expenses
    SET StatusId    = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted'),
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- Approve or Reject expense (manager action)
CREATE OR ALTER PROCEDURE dbo.usp_UpdateExpenseStatus
    @ExpenseId   INT,
    @StatusId    INT,
    @ReviewedBy  INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Expenses
    SET StatusId   = @StatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- Delete expense (draft only)
CREATE OR ALTER PROCEDURE dbo.usp_DeleteExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.Expenses WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- USERS
-- =============================================

-- Get all active users
CREATE OR ALTER PROCEDURE dbo.usp_GetUsers
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r        ON u.RoleId    = r.RoleId
    LEFT JOIN dbo.Users m   ON u.ManagerId = m.UserId
    WHERE (@IncludeInactive = 1 OR u.IsActive = 1)
    ORDER BY u.UserName;
END
GO

-- Get single user by ID
CREATE OR ALTER PROCEDURE dbo.usp_GetUserById
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r        ON u.RoleId    = r.RoleId
    LEFT JOIN dbo.Users m   ON u.ManagerId = m.UserId
    WHERE u.UserId = @UserId;
END
GO

-- Create user
CREATE OR ALTER PROCEDURE dbo.usp_CreateUser
    @UserName  NVARCHAR(100),
    @Email     NVARCHAR(255),
    @RoleId    INT,
    @ManagerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Users (UserName, Email, RoleId, ManagerId, IsActive, CreatedAt)
    VALUES (@UserName, @Email, @RoleId, @ManagerId, 1, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS UserId;
END
GO

-- Update user
CREATE OR ALTER PROCEDURE dbo.usp_UpdateUser
    @UserId    INT,
    @UserName  NVARCHAR(100),
    @Email     NVARCHAR(255),
    @RoleId    INT,
    @ManagerId INT = NULL,
    @IsActive  BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users
    SET UserName  = @UserName,
        Email     = @Email,
        RoleId    = @RoleId,
        ManagerId = @ManagerId,
        IsActive  = @IsActive
    WHERE UserId = @UserId;
END
GO

-- Delete (deactivate) user
CREATE OR ALTER PROCEDURE dbo.usp_DeleteUser
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET IsActive = 0 WHERE UserId = @UserId;
END
GO

-- =============================================
-- CATEGORIES
-- =============================================

-- Get all categories
CREATE OR ALTER PROCEDURE dbo.usp_GetCategories
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE (@IncludeInactive = 1 OR IsActive = 1)
    ORDER BY CategoryName;
END
GO

-- Create category
CREATE OR ALTER PROCEDURE dbo.usp_CreateCategory
    @CategoryName NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ExpenseCategories (CategoryName, IsActive)
    VALUES (@CategoryName, 1);

    SELECT SCOPE_IDENTITY() AS CategoryId;
END
GO

-- Update category
CREATE OR ALTER PROCEDURE dbo.usp_UpdateCategory
    @CategoryId   INT,
    @CategoryName NVARCHAR(100),
    @IsActive     BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ExpenseCategories
    SET CategoryName = @CategoryName,
        IsActive     = @IsActive
    WHERE CategoryId = @CategoryId;
END
GO

-- Delete category
CREATE OR ALTER PROCEDURE dbo.usp_DeleteCategory
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ExpenseCategories SET IsActive = 0 WHERE CategoryId = @CategoryId;
END
GO

-- =============================================
-- LOOKUP DATA
-- =============================================

-- Get all roles
CREATE OR ALTER PROCEDURE dbo.usp_GetRoles
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RoleId, RoleName, Description FROM dbo.Roles ORDER BY RoleName;
END
GO

-- Get all expense statuses
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseStatuses
AS
BEGIN
    SET NOCOUNT ON;
    SELECT StatusId, StatusName FROM dbo.ExpenseStatus ORDER BY StatusId;
END
GO

-- =============================================
-- DASHBOARD SUMMARY
-- =============================================

-- Get expense summary counts grouped by status
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseSummary
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        s.StatusName,
        COUNT(e.ExpenseId)        AS ExpenseCount,
        ISNULL(SUM(e.AmountMinor), 0) AS TotalAmountMinor
    FROM dbo.ExpenseStatus s
    LEFT JOIN dbo.Expenses e ON e.StatusId = s.StatusId
    GROUP BY s.StatusId, s.StatusName
    ORDER BY s.StatusId;
END
GO

using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using ExpenseApp.Models;
using System.Text.Json;
using System.ClientModel;

namespace ExpenseApp.Services;

public class ChatService
{
    private readonly DatabaseService _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;

    public ChatService(DatabaseService db, IConfiguration configuration, ILogger<ChatService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsGenAIEnabled
    {
        get
        {
            var endpoint = _configuration["OpenAI:Endpoint"];
            return !string.IsNullOrEmpty(endpoint);
        }
    }

    public async Task<string> ChatAsync(string userMessage)
    {
        if (!IsGenAIEnabled)
        {
            return "The GenAI services have not been deployed. Please run './deploy-with-chat.sh' to deploy Azure OpenAI and AI Search resources. This will enable the full chat experience with real data queries.";
        }

        try
        {
            var endpoint = _configuration["OpenAI:Endpoint"]!;
            var deploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-4o";
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];

            Azure.Core.TokenCredential credential;
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            var client = new AzureOpenAIClient(new Uri(endpoint), credential);
            var chatClient = client.GetChatClient(deploymentName);

            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool("get_expenses", "Get expenses from the database with optional filters",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "statusId": { "type": "integer", "description": "Filter by status: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected" },
                            "userId": { "type": "integer", "description": "Filter by user ID" },
                            "categoryId": { "type": "integer", "description": "Filter by category ID" }
                        }
                    }
                    """)),
                ChatTool.CreateFunctionTool("get_users", "Get all users from the system",
                    BinaryData.FromString("""{"type":"object","properties":{}}""")),
                ChatTool.CreateFunctionTool("get_categories", "Get all expense categories",
                    BinaryData.FromString("""{"type":"object","properties":{}}""")),
                ChatTool.CreateFunctionTool("get_expense_summary", "Get expense summary counts grouped by status",
                    BinaryData.FromString("""{"type":"object","properties":{}}""")),
                ChatTool.CreateFunctionTool("create_expense", "Create a new expense",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "required": ["userId", "categoryId", "amountMinor", "expenseDate"],
                        "properties": {
                            "userId": { "type": "integer" },
                            "categoryId": { "type": "integer" },
                            "amountMinor": { "type": "integer", "description": "Amount in pence (e.g. £12.50 = 1250)" },
                            "expenseDate": { "type": "string", "format": "date" },
                            "description": { "type": "string" }
                        }
                    }
                    """)),
                ChatTool.CreateFunctionTool("approve_expense", "Approve an expense",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "required": ["expenseId", "reviewedBy"],
                        "properties": {
                            "expenseId": { "type": "integer" },
                            "reviewedBy": { "type": "integer", "description": "Manager user ID" }
                        }
                    }
                    """)),
                ChatTool.CreateFunctionTool("reject_expense", "Reject an expense",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "required": ["expenseId", "reviewedBy"],
                        "properties": {
                            "expenseId": { "type": "integer" },
                            "reviewedBy": { "type": "integer" }
                        }
                    }
                    """))
            };

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("""
                    You are a helpful AI assistant for an Expense Management System.
                    You have access to functions to query and manage expenses, users, and categories.
                    When asked to list things, provide clear formatted lists.
                    Amounts are stored in pence (minor units), so divide by 100 to get GBP values.
                    Available status IDs: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected.
                    """),
                new UserChatMessage(userMessage)
            };

            var options = new ChatCompletionOptions();
            foreach (var tool in tools) options.Tools.Add(tool);

            // Function calling loop
            ChatCompletion response;
            while (true)
            {
                response = await chatClient.CompleteChatAsync(messages, options);

                if (response.FinishReason == ChatFinishReason.ToolCalls)
                {
                    messages.Add(new AssistantChatMessage(response));

                    var toolResults = new List<ToolChatMessage>();
                    foreach (var toolCall in response.ToolCalls)
                    {
                        var result = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                        toolResults.Add(new ToolChatMessage(toolCall.Id, result));
                    }
                    messages.AddRange(toolResults);
                }
                else
                {
                    break;
                }
            }

            return response.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat error");
            return $"Sorry, I encountered an error: {ex.Message}. Please check the Azure OpenAI configuration.";
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var args = JsonDocument.Parse(arguments).RootElement;

            switch (functionName)
            {
                case "get_expenses":
                {
                    int? statusId = args.TryGetProperty("statusId", out var s) ? s.GetInt32() : null;
                    int? userId = args.TryGetProperty("userId", out var u) ? u.GetInt32() : null;
                    int? categoryId = args.TryGetProperty("categoryId", out var c) ? c.GetInt32() : null;
                    var expenses = await _db.GetExpensesAsync(statusId, userId, categoryId);
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId, e.UserName, e.CategoryName, e.StatusName,
                        AmountGBP = e.AmountGBP.ToString("F2"),
                        ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                        e.Description
                    }));
                }
                case "get_users":
                {
                    var users = await _db.GetUsersAsync();
                    return JsonSerializer.Serialize(users.Select(u => new { u.UserId, u.UserName, u.Email, u.RoleName, u.IsActive }));
                }
                case "get_categories":
                {
                    var cats = await _db.GetCategoriesAsync();
                    return JsonSerializer.Serialize(cats);
                }
                case "get_expense_summary":
                {
                    var summary = await _db.GetExpenseSummaryAsync();
                    return JsonSerializer.Serialize(summary.Select(s => new
                    {
                        s.StatusName, s.ExpenseCount,
                        TotalGBP = s.TotalAmountGBP.ToString("F2")
                    }));
                }
                case "create_expense":
                {
                    var userId = args.GetProperty("userId").GetInt32();
                    var categoryId = args.GetProperty("categoryId").GetInt32();
                    var amountMinor = args.GetProperty("amountMinor").GetInt32();
                    var expenseDate = DateTime.Parse(args.GetProperty("expenseDate").GetString()!);
                    var description = args.TryGetProperty("description", out var d) ? d.GetString() : null;
                    var id = await _db.CreateExpenseAsync(userId, categoryId, amountMinor, "GBP", expenseDate, description, null);
                    return JsonSerializer.Serialize(new { success = id > 0, expenseId = id });
                }
                case "approve_expense":
                {
                    var expenseId = args.GetProperty("expenseId").GetInt32();
                    var reviewedBy = args.GetProperty("reviewedBy").GetInt32();
                    await _db.UpdateExpenseStatusAsync(expenseId, 3, reviewedBy);
                    return JsonSerializer.Serialize(new { success = true, message = "Expense approved" });
                }
                case "reject_expense":
                {
                    var expenseId = args.GetProperty("expenseId").GetInt32();
                    var reviewedBy = args.GetProperty("reviewedBy").GetInt32();
                    await _db.UpdateExpenseStatusAsync(expenseId, 4, reviewedBy);
                    return JsonSerializer.Serialize(new { success = true, message = "Expense rejected" });
                }
                default:
                    return $"{{\"error\": \"Unknown function: {functionName}\"}}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Function execution error for {Function}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}

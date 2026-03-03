using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseApp.Pages;

public class ChatModel : PageModel
{
    private readonly IConfiguration _configuration;

    public ChatModel(IConfiguration configuration) => _configuration = configuration;

    public bool IsGenAIEnabled { get; set; }

    public void OnGet()
    {
        var endpoint = _configuration["OpenAI:Endpoint"];
        IsGenAIEnabled = !string.IsNullOrEmpty(endpoint);
    }
}

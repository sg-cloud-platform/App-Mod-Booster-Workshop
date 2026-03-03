using Microsoft.AspNetCore.Mvc;
using ExpenseApp.Services;

namespace ExpenseApp.Controllers;

/// <summary>Chat AI API</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;

    public ChatController(ChatService chatService) => _chatService = chatService;

    /// <summary>Send a message to the AI assistant</summary>
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrEmpty(request.Message))
            return BadRequest("Message is required");

        var response = await _chatService.ChatAsync(request.Message);
        return Ok(new ChatResponse(response));
    }
}

public record ChatRequest(string Message);
public record ChatResponse(string Response);

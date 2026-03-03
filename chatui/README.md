# Chat UI

The Chat UI is embedded directly within the main ASP.NET application at the `/Chat` route.

It uses:
- **Azure OpenAI GPT-4o** via `Azure.AI.OpenAI` SDK with `ManagedIdentityCredential`
- **Function calling** (`ChatTool.CreateFunctionTool`) to query/update the expense database via APIs
- **RAG pattern** with Azure AI Search for contextual responses

## Files

| File | Description |
|------|-------------|
| `../app/Pages/Chat.cshtml` | Razor Page - Chat UI frontend |
| `../app/Pages/Chat.cshtml.cs` | Page model - checks if GenAI is configured |
| `../app/Services/ChatService.cs` | Chat service with function calling loop |
| `../app/Services/GenAISettings.cs` | Configuration model for GenAI settings |
| `../app/Controllers/ChatController.cs` | REST API endpoint `/api/chat` |

## How It Works

1. User sends a message via the chat UI at `/Chat`
2. The frontend calls `POST /api/chat` with the message
3. `ChatService` sends the message to Azure OpenAI with function tool definitions
4. If OpenAI calls a function (e.g. `get_expenses`), the service executes it against the DB
5. Results are sent back to OpenAI which formulates a natural language response
6. Response is displayed in the chat UI with formatted HTML (bold, lists, etc.)

## Available Functions

| Function | Description |
|----------|-------------|
| `get_expenses` | List expenses with optional status/user/category filters |
| `get_users` | List all users |
| `get_categories` | List all categories |
| `get_expense_summary` | Summary counts by status |
| `create_expense` | Create a new expense |
| `approve_expense` | Approve a submitted expense |
| `reject_expense` | Reject a submitted expense |

## Demo Mode

If GenAI services are not deployed (`OpenAI:Endpoint` is empty in config), the chat will return a helpful message explaining how to deploy them:

```
The GenAI services have not been deployed. Please run './deploy-with-chat.sh' 
to deploy Azure OpenAI and AI Search resources.
```

## Configuration

Set these App Service application settings after running `deploy-with-chat.sh`:

```
OpenAI__Endpoint          = https://oai-expensemgmt-xxx.openai.azure.com/
OpenAI__DeploymentName    = gpt-4o
ManagedIdentityClientId   = <client-id-of-user-assigned-MI>
AZURE_CLIENT_ID           = <client-id-of-user-assigned-MI>
```

namespace ExpenseApp.Services;

public class GenAISettings
{
    public string? Endpoint { get; set; }
    public string? DeploymentName { get; set; }
    public string? SearchEndpoint { get; set; }
    public string? ManagedIdentityClientId { get; set; }

    public bool IsConfigured => !string.IsNullOrEmpty(Endpoint) && !string.IsNullOrEmpty(DeploymentName);
}

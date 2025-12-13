namespace AISignoffBot.Models;

public class JiraOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ApiToken { get; init; } = string.Empty;
    public string AiStatusName { get; init; } = "AI Signoff";
    public string QaStatusName { get; init; } = "QA";
    public string TriggerTag { get; init; } = "@ai";
    public int MaxEvidenceImages { get; init; } = 3;
}
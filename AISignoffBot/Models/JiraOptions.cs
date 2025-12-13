namespace AISignoffBot.Models;

public class JiraOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string AiStatusName { get; set; } = "AI Signoff";
    public string QaStatusName { get; set; } = "QA";
    public string TriggerTag { get; set; } = "@ai";
    public int MaxEvidenceImages { get; set; } = 3;
}
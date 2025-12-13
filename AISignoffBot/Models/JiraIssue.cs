namespace AISignoffBot.Models;

public record JiraIssue(
    string Key,
    string Summary,
    string Description,
    IReadOnlyList<string> Labels,
    IReadOnlyList<JiraAttachment> Attachments);

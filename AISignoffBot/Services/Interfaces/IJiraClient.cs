using AISignoffBot.Models;

namespace AISignoffBot.Services.Interfaces;

public interface IJiraClient
{
    Task AddComment(string issueKey, string comment, CancellationToken ct = default);
    Task<JiraIssue> GetIssue(string issueKey, CancellationToken ct = default);
    Task AddLabels(string issueKey, IEnumerable<string> labels, CancellationToken ct = default);
    Task SetLabels(string issueKey, IEnumerable<string> labels, CancellationToken ct = default);
    Task TransitionToStatus(string issueKey, string targetStatusName, CancellationToken ct = default);
    Task SetFlagged(string issueKey, bool flagged, CancellationToken ct = default);
    Task Assign(string issueKey, string accountId, CancellationToken ct = default);
    Task AssignToBot(string issueKey, CancellationToken ct = default);
}
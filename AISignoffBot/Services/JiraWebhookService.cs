using System.Text.Json;
using AISignoffBot.Enums;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AISignoffBot.Services;

public class JiraWebhookService(
    ILogger<JiraWebhookService> logger,
    ISignoffWorkflow workflow,
    IOptions<JiraOptions> options) : IJiraWebhookService
{
    private readonly JiraOptions _options = options.Value;

    public async Task HandleWebhook(string payload)
    {
        logger.LogDebug("Webhook payload: {Payload}", payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var trigger = TryGetTrigger(root);
        if (trigger is null)
        {
            logger.LogInformation("No trigger detected.");
            return;
        }

        logger.LogInformation("Trigger detected: {IssueKey} via {Type}", trigger.IssueKey, trigger.Type);

        await workflow.Process(trigger.IssueKey, trigger.Type, trigger.ActorAccountId);
    }
    
    private JiraTrigger? TryGetTrigger(JsonElement root)
    {
        var webhookEvent = root.GetProperty("webhookEvent").GetString();
        var issueKey = root.GetProperty("issue").GetProperty("key").GetString();

        if (string.IsNullOrWhiteSpace(issueKey)) return null;
        
        var actor = TryGetActorAccountId(root);

        // Status change → AI Signoff
        if (webhookEvent == "jira:issue_updated" && root.TryGetProperty("changelog", out var changelog))
        {
            foreach (var item in changelog.GetProperty("items").EnumerateArray())
            {
                if (item.GetProperty("field").GetString() == "status")
                {
                    var fromStatus = item.GetProperty("fromString").GetString();
                    var toStatus = item.GetProperty("toString").GetString();

                    if (string.Equals(fromStatus, toStatus, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.Equals(toStatus, _options.AiStatusName, StringComparison.OrdinalIgnoreCase))
                        return new JiraTrigger(issueKey, JiraTriggerType.StatusChangeToAi, actor);

                    if (string.Equals(toStatus, _options.QaStatusName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(fromStatus, _options.AiStatusName, StringComparison.OrdinalIgnoreCase))
                        return new JiraTrigger(issueKey, JiraTriggerType.StatusChangeToQa, actor);
                }
            }
        }

        // Comment with trigger tag
        if (webhookEvent == "comment_created" && root.TryGetProperty("comment", out var comment))
        {
            var bodyText = comment.GetProperty("body").GetString() ?? string.Empty;
            if (bodyText.Contains(_options.TriggerTag, StringComparison.OrdinalIgnoreCase))
            {
                return new JiraTrigger(issueKey, JiraTriggerType.CommentTag);
            }
        }

        return null;
    }
    
    private static string? TryGetActorAccountId(JsonElement root)
    {
        if (root.TryGetProperty("user", out var user) &&
            user.TryGetProperty("accountId", out var acc))
            return acc.GetString();

        return null;
    }

}
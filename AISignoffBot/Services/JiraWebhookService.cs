using System.Text.Json;
using AISignoffBot.Models;
using Microsoft.Extensions.Options;

namespace AISignoffBot.Services;

public class JiraWebhookService(
    ILogger<JiraWebhookService> logger,
    IJiraClient jiraClient,
    IOptions<JiraOptions> options) : IJiraWebhookService
{
    private readonly JiraOptions _options = options.Value;

    public async Task HandleWebhook(string payload)
    {
        logger.LogDebug("Webhook payload: {Payload}", payload);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var webhookEvent = root.GetProperty("webhookEvent").GetString();
        var issueKey = root.GetProperty("issue").GetProperty("key").GetString();

        if (string.IsNullOrWhiteSpace(issueKey))
        {
            logger.LogWarning("No issue key in webhook payload.");
            return;
        }

        var isTrigger = false;

        // Status change → AI Signoff
        if (webhookEvent == "jira:issue_updated" &&
            root.TryGetProperty("changelog", out var changelog))
        {
            foreach (var item in changelog.GetProperty("items").EnumerateArray())
            {
                if (item.GetProperty("field").GetString() == "status")
                {
                    var fromStatus = item.GetProperty("fromString").GetString();
                    var toStatus = item.GetProperty("toString").GetString();

                    // trigger ONLY when actually moved into AI Signoff
                    if (!string.Equals(fromStatus, toStatus, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(toStatus, _options.AiStatusName, StringComparison.OrdinalIgnoreCase))
                    {
                        isTrigger = true;
                        break;
                    }
                }
            }
        }


        // Comment with trigger tag
        if (!isTrigger &&
            webhookEvent == "comment_created" &&
            root.TryGetProperty("comment", out var comment))
        {
            var bodyText = comment.GetProperty("body").GetString() ?? string.Empty;

            if (bodyText.Contains(_options.TriggerTag, StringComparison.OrdinalIgnoreCase))
            {
                isTrigger = true;
            }
        }

        if (!isTrigger)
        {
            logger.LogInformation("No AI trigger detected for issue {IssueKey}", issueKey);
            return;
        }

        logger.LogInformation("AI trigger detected for issue {IssueKey}", issueKey);

        // V0 behaviour: just add a dummy comment
        await jiraClient.AddComment(
            issueKey,
            "[AI BOT] Placeholder: AI sign-off trigger received. (V0 dummy response)");
    }
}
using AISignoffBot.Enums;
using AISignoffBot.Services.Interfaces;

namespace AISignoffBot.Services;

public class SignoffWorkflow(
    ILogger<SignoffWorkflow> logger,
    IJiraClient jira,
    IAcProvider acProvider,
    IEvidenceProvider evidenceProvider,
    IAiEvidenceAnalyzer aiEvidenceAnalyzer,
    ISignoffReporter reporter)
    : ISignoffWorkflow
{
    private const string LabelProcessed = "ai_processed";
    private const string LabelSignedOff = "ai_signed_off";
    private const string LabelNeedsHuman = "needs_human_review";
    private const string LabelAddressing = "addressing_ai_feedback";
    private const string LabelProcessing = "ai_processing_in_progress";
    
    private static readonly string[] AiLabels =
    [
        LabelProcessed,
        LabelSignedOff,
        LabelNeedsHuman,
        LabelAddressing,
        LabelProcessing
    ];

    public async Task Process(string issueKey, JiraTriggerType triggerType, string? actorAccountId, CancellationToken ct = default)
    {
        if (triggerType == JiraTriggerType.StatusChangeToQa)
        {
            if (!string.IsNullOrWhiteSpace(actorAccountId))
            {
                await jira.Assign(issueKey, actorAccountId, ct);
            }
            
            await ResetForQaAsync(issueKey, ct);
            return;
        }
        
        logger.LogInformation("Processing signoff for {IssueKey} (Trigger: {Trigger})", issueKey, triggerType);

        // 1) Read issue, remove addressing label, assign to bot
        var issue = await jira.GetIssue(issueKey, ct);
        var cleanedLabels = issue.Labels
            .Where(l => !string.Equals(l, LabelAddressing, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await jira.SetLabels(issueKey, cleanedLabels, ct);
        await jira.AssignToBot(issueKey, ct);

        // 2) Idempotency: if already processed, do nothing
        if (issue.Labels.Any(l => string.Equals(l, LabelProcessed, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogInformation("Issue {IssueKey} already processed (label present). Skipping.", issueKey);
            return;
        }

        await jira.AddLabels(issueKey, [LabelProcessing], ct);

        try
        {
            // 3) Extract ACs
            var acs = acProvider.ExtractAcceptanceCriteria(issue.Description);

            // 4) Gather evidence
            var evidenceImages = await evidenceProvider.GetLatestImagesAsync(issueKey, ct);
            var evidenceComment = reporter.FormatEvidenceComment(evidenceImages);
            await jira.AddComment(issueKey, evidenceComment, ct);

            // 5) Evaluate (stub for V1)
            var result = await aiEvidenceAnalyzer.AnalyzeAsync(acs, evidenceImages, ct);

            // 6) Comment
            var comment = reporter.FormatComment(issue, acs, evidenceImages, result);
            await jira.AddComment(issueKey, comment, ct);

            // 7) Mark processed (prevents loops)
            await jira.AddLabels(issueKey, [LabelProcessed], ct);

            // 8) Outcome actions
            if (result.Passed)
            {
                await jira.AddLabels(issueKey, [LabelSignedOff], ct);

                // transition name can be config-driven later; for now "Done"
                await jira.TransitionToStatus(issueKey, "Done", ct);
                await jira.SetFlagged(issueKey, false, ct);

            }
            else
            {
                await jira.AddLabels(issueKey, [LabelNeedsHuman], ct);
                await jira.SetFlagged(issueKey, true, ct);
                // Optional: move back to In Progress, or keep in AI Signoff
                // await _jira.TransitionToStatus(issueKey, "In Progress", ct);
            }

            logger.LogInformation("Completed signoff for {IssueKey} (Passed: {Passed})", issueKey, result.Passed);
        }
        finally
        {
            await RemoveProcessingLabel(issueKey, ct);
        }
    }

    private async Task ResetForQaAsync(string issueKey, CancellationToken ct)
    {
        logger.LogInformation("Resetting AI state for {IssueKey} (moved to QA)", issueKey);

        var issue = await jira.GetIssue(issueKey, ct);

        // Remove all AI labels
        var remaining = issue.Labels
            .Where(l => !AiLabels.Contains(l, StringComparer.OrdinalIgnoreCase))
            .ToList();

        // Add addressing label
        remaining.Add(LabelAddressing);

        await jira.SetFlagged(issueKey, false, ct);
        await jira.SetLabels(issueKey, remaining, ct);

        await jira.AddComment(
            issueKey,
            "[AI BOT] Cleared AI review state (flag + labels) as issue moved back to QA. Label added: addressing_ai_feedback.",
            ct);
    }

    private async Task RemoveProcessingLabel(string issueKey, CancellationToken ct)
    {
        var issue = await jira.GetIssue(issueKey, ct);

        var remaining = issue.Labels
            .Where(l => !string.Equals(l, LabelProcessing, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (remaining.Count == issue.Labels.Count)
        {
            return;
        }

        await jira.SetLabels(issueKey, remaining, ct);
    }

}

using System.Text;
using AISignoffBot.Enums;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;

namespace AISignoffBot.Services.V1;

public class MarkdownSignoffReporter : ISignoffReporter
{
    public string FormatComment(JiraIssue issue, IReadOnlyList<string> acceptanceCriteria, SignoffResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[AI BOT] AC Signoff (V1 Stub)");
        sb.AppendLine($"Issue: {issue.Key} - {issue.Summary}");
        sb.AppendLine($"Result: {(result.Passed ? "PASS ✅" : "FAIL ❌")}");
        sb.AppendLine();

        if (acceptanceCriteria.Count == 0)
        {
            sb.AppendLine("No acceptance criteria found in the description.");
            return sb.ToString();
        }

        sb.AppendLine("Acceptance Criteria:");
        foreach (var r in result.CriteriaResults)
        {
            var icon = r.Status switch
            {
                AcStatus.Met => "✅",
                AcStatus.NotMet => "❌",
                _ => "⚠️"
            };

            sb.AppendLine($"- {icon} {r.Criterion} — {r.Status} ({r.Notes})");
        }

        sb.AppendLine();
        sb.AppendLine("Note: V1 uses a stub evaluator (randomised). AI analysis will replace this.");

        return sb.ToString();
    }
}
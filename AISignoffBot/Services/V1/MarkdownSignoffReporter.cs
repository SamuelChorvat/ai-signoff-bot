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

    public string FormatEvidenceComment(IReadOnlyList<EvidenceImage> evidenceImages)
    {
        var sb = new StringBuilder();

        if (evidenceImages.Count == 0)
        {
            sb.Append("[AI BOT] Evidence: no images found.");
            return sb.ToString();
        }

        sb.AppendLine($"[AI BOT] Evidence: found {evidenceImages.Count} image(s)");

        foreach (var image in evidenceImages.OrderBy(e => e.Index))
        {
            sb.AppendLine($"- img{image.Index + 1}: {image.Filename} ({FormatKilobytes(image.Bytes.Length)})");
        }

        return sb.ToString();
    }

    private static string FormatKilobytes(int byteCount)
    {
        if (byteCount <= 0)
        {
            return "0KB";
        }

        var kb = byteCount / 1024d;
        return kb < 0.1 ? $"{byteCount}B" : $"{Math.Round(kb, 1)}KB";
    }
}

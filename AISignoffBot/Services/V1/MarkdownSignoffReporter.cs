using System.Linq;
using System.Text;
using AISignoffBot.Enums;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;

namespace AISignoffBot.Services.V1;

public class MarkdownSignoffReporter : ISignoffReporter
{
    public string FormatComment(
        JiraIssue issue,
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<EvidenceImage> evidenceImages,
        SignoffResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[AI BOT] AC Signoff (Vision AI)");
        sb.AppendLine($"Issue: {issue.Key} - {issue.Summary}");
        sb.AppendLine($"Result: {(result.Passed ? "PASS ✅" : "FAIL ❌")}");
        sb.AppendLine();

        if (acceptanceCriteria.Count == 0)
        {
            sb.AppendLine("No acceptance criteria found in the description.");
            return sb.ToString();
        }

        sb.AppendLine("Acceptance Criteria:");

        var evidenceLookup = evidenceImages
            .ToLookup(img => img.Filename, img => img.AttachmentUrl, StringComparer.OrdinalIgnoreCase);

        foreach (var r in result.CriteriaResults)
        {
            var icon = r.Status switch
            {
                AcStatus.Met => "✅",
                AcStatus.NotMet => "❌",
                _ => "⚠️"
            };

            var evidenceText = FormatEvidenceLinks(r.Evidence, evidenceLookup);
            var line = $"- {icon} {r.Criterion} — {r.Status} ({r.Notes})";

            if (!string.IsNullOrEmpty(evidenceText))
            {
                line += $" Evidence: {evidenceText}";
            }

            sb.AppendLine(line);
        }

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
            var attachment = FormatEvidenceLink(image.Filename, image.AttachmentUrl);
            sb.AppendLine($"- {attachment} ({FormatKilobytes(image.Bytes.Length)})");
        }

        return sb.ToString();
    }

    private static string FormatEvidenceLinks(
        IReadOnlyList<string> evidence,
        ILookup<string, string> evidenceLookup)
    {
        if (evidence.Count == 0)
        {
            return string.Empty;
        }

        var links = new List<string>(evidence.Count);

        foreach (var filename in evidence)
        {
            var attachmentUrl = evidenceLookup[filename]
                .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

            links.Add(FormatEvidenceLink(filename, attachmentUrl));
        }

        return string.Join(", ", links);
    }

    private static string FormatEvidenceLink(string filename, string? url)
    {
        return string.IsNullOrWhiteSpace(url)
            ? filename
            : $"[{filename}]({url})";
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

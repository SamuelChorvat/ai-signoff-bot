using AISignoffBot.Models;

namespace AISignoffBot.Services.Interfaces;

public interface ISignoffReporter
{
    string FormatComment(
        JiraIssue issue,
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<EvidenceImage> evidenceImages,
        SignoffResult result,
        RuleResult ruleResult,
        TimeSpan processingTime);

    string FormatEvidenceComment(IReadOnlyList<EvidenceImage> evidenceImages);
}

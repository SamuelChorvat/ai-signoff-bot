using AISignoffBot.Models;

namespace AISignoffBot.Services.Interfaces;

public interface ISignoffReporter
{
    string FormatComment(
        JiraIssue issue,
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<EvidenceImage> evidenceImages,
        SignoffResult result);

    string FormatEvidenceComment(IReadOnlyList<EvidenceImage> evidenceImages);
}

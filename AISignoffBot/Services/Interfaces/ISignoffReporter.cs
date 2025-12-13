using AISignoffBot.Models;

namespace AISignoffBot.Services.Interfaces;

public interface ISignoffReporter
{
    string FormatComment(JiraIssue issue, IReadOnlyList<string> acceptanceCriteria, SignoffResult result);

    string FormatEvidenceComment(IReadOnlyList<EvidenceImage> evidenceImages);
}

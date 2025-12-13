using AISignoffBot.Models;

namespace AISignoffBot.Services.Interfaces;

public interface IAiEvidenceAnalyzer
{
    Task<SignoffResult> AnalyzeAsync(
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<EvidenceImage> evidenceImages,
        CancellationToken ct = default);
}

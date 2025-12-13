using AISignoffBot.Models;

namespace AISignoffBot.Services.Interfaces;

public interface IAiClient
{
    Task<string> GetVisionAnalysisAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<EvidenceImage> evidenceImages,
        CancellationToken ct = default);
}

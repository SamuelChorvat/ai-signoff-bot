using AISignoffBot.Models;

namespace AISignoffBot.Services.Interfaces;

public interface IEvidenceProvider
{
    Task<IReadOnlyList<EvidenceImage>> GetLatestImagesAsync(string issueKey, CancellationToken ct = default);
}

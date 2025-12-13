using AISignoffBot.Models;

namespace AISignoffBot.Services.Interfaces;

public interface ISignoffRule
{
    Task<RuleResult> EvaluateAsync(IReadOnlyList<EvidenceImage> evidenceImages, CancellationToken ct = default);
}

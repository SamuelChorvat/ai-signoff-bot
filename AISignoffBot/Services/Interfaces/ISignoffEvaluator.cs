using AISignoffBot.Models;

namespace AISignoffBot.Services.Interfaces;

public interface ISignoffEvaluator
{
    Task<SignoffResult> EvaluateAsync(IReadOnlyList<string> acceptanceCriteria, CancellationToken ct = default);
}
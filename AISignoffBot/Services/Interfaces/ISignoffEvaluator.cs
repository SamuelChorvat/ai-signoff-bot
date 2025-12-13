using AISignoffBot.Models;

namespace AISignoffBot.Services.Interfaces;

public interface ISignoffEvaluator
{
    Task<SignoffResult> EvaluateAsync(
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<JiraAttachmentEvidence> evidence,
        CancellationToken ct = default);
}
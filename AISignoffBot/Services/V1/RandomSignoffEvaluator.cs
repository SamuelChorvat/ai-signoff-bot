using AISignoffBot.Enums;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;

namespace AISignoffBot.Services.V1;

public class RandomSignoffEvaluator : ISignoffEvaluator
{
    private readonly Random _rng = new();

    public Task<SignoffResult> EvaluateAsync(
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<JiraAttachmentEvidence> evidence,
        CancellationToken ct = default)
    {
        var passed = _rng.NextDouble() >= 0.5;

        // For demo: if passed, mark all Met; if fail, mark half NotMet
        var results = new List<AcResult>();

        for (var i = 0; i < acceptanceCriteria.Count; i++)
        {
            var status = passed
                ? AcStatus.Met
                : (i % 2 == 0 ? AcStatus.NotMet : AcStatus.NoEvidence);

            results.Add(new AcResult(
                acceptanceCriteria[i],
                status,
                passed ? "Stub pass" : "Stub fail / missing evidence"));
        }

        return Task.FromResult(new SignoffResult(passed, results));
    }
}
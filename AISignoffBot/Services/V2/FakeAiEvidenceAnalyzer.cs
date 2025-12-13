using AISignoffBot.Enums;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;

namespace AISignoffBot.Services.V2;

public class FakeAiEvidenceAnalyzer : IAiEvidenceAnalyzer
{
    public Task<SignoffResult> AnalyzeAsync(
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<EvidenceImage> evidenceImages,
        CancellationToken ct = default)
    {
        var results = new List<AcResult>();

        for (var i = 0; i < acceptanceCriteria.Count; i++)
        {
            var status = i == 0 ? AcStatus.Met : AcStatus.NoEvidence;
            var notes = status == AcStatus.Met
                ? "Fake AI: automatically marked as Met."
                : "Fake AI: no evidence provided for this criterion.";

            results.Add(new AcResult(acceptanceCriteria[i], status, notes));
        }

        var passed = results.All(r => r.Status == AcStatus.Met);

        return Task.FromResult(new SignoffResult(passed, results));
    }
}

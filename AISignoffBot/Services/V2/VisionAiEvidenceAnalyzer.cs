using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AISignoffBot.Enums;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AISignoffBot.Services.V2;

public class VisionAiEvidenceAnalyzer(
    ILogger<VisionAiEvidenceAnalyzer> logger,
    IAiClient aiClient,
    IOptions<AiOptions> aiOptions)
    : IAiEvidenceAnalyzer
{
    private readonly AiOptions options = aiOptions.Value;

    public async Task<SignoffResult> AnalyzeAsync(
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<EvidenceImage> evidenceImages,
        CancellationToken ct = default)
    {
        if (acceptanceCriteria.Count == 0)
        {
            return new SignoffResult(false, []);
        }

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(acceptanceCriteria, evidenceImages);

        logger.LogInformation("Running vision analysis with model {Model}", options.Model);

        try
        {
            var raw = await aiClient.GetVisionAnalysisAsync(systemPrompt, userPrompt, evidenceImages, ct);
            var parsed = ParseResponse(raw, acceptanceCriteria.Count);

            if (parsed == null)
            {
                return BuildInvalidResult(acceptanceCriteria, "AI output invalid");
            }

            var results = BuildResults(acceptanceCriteria, parsed);
            var passed = results.All(r => r.Status == AcStatus.Met);

            return new SignoffResult(passed, results);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "AI returned invalid JSON");
            return BuildInvalidResult(acceptanceCriteria, "AI output invalid");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI analysis failed");
            return BuildInvalidResult(acceptanceCriteria, "AI output invalid");
        }
    }

    private static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert QA assistant that verifies acceptance criteria using provided screenshots only.");
        sb.AppendLine("You will receive images indexed img1..imgN.");
        sb.AppendLine("Do not depend on image file names or assume they relate to the story; only trust visible content.");
        sb.AppendLine("If a criterion is not clearly visible in any image, set the status to NoEvidence.");

        return sb.ToString();
    }

    private static string BuildUserPrompt(
        IReadOnlyList<string> acceptanceCriteria,
        IReadOnlyList<EvidenceImage> evidenceImages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Evaluate each acceptance criterion using the provided images.");
        sb.AppendLine("Use only what is visible; do not infer beyond the screenshots.");
        sb.AppendLine();

        sb.AppendLine("Acceptance Criteria:");
        for (var i = 0; i < acceptanceCriteria.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {acceptanceCriteria[i]}");
        }

        sb.AppendLine();
        sb.AppendLine("Images:");
        if (evidenceImages.Count == 0)
        {
            sb.AppendLine("No images available.");
        }
        else
        {
            foreach (var image in evidenceImages.OrderBy(i => i.Index))
            {
                sb.AppendLine($"- img{image.Index + 1}: screenshot available");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Return strict JSON with this shape:");
        sb.AppendLine("{");
        sb.AppendLine("  \"passed\": true|false,  // true only if every AC is Met");
        sb.AppendLine("  \"criteria\": [");
        sb.AppendLine("    { \"index\": 1, \"status\": \"Met|NotMet|NoEvidence\", \"notes\": \"short reason\", \"evidence\": [\"img1\"] }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("Use NoEvidence when the requirement is not visible. Reference images by img1, img2, etc.");

        return sb.ToString();
    }

    private static IReadOnlyList<AcResult> BuildResults(
        IReadOnlyList<string> acceptanceCriteria,
        AiResponse parsed)
    {
        var results = new List<AcResult>();

        for (var i = 0; i < acceptanceCriteria.Count; i++)
        {
            var acIndex = i + 1;
            var match = parsed.Criteria.FirstOrDefault(c => c.Index == acIndex);

            if (match == null)
            {
                results.Add(new AcResult(acceptanceCriteria[i], AcStatus.NoEvidence, "AI did not return a result for this criterion."));
                continue;
            }

            var status = match.Status.ToLowerInvariant() switch
            {
                "met" => AcStatus.Met,
                "notmet" => AcStatus.NotMet,
                _ => AcStatus.NoEvidence
            };

            var notes = string.IsNullOrWhiteSpace(match.Notes)
                ? "No explanation provided."
                : match.Notes.Trim();

            if (match.Evidence?.Count > 0)
            {
                notes = $"{notes} Evidence: {string.Join(", ", match.Evidence)}";
            }

            results.Add(new AcResult(acceptanceCriteria[i], status, notes));
        }

        return results;
    }

    private static SignoffResult BuildInvalidResult(IReadOnlyList<string> acceptanceCriteria, string note)
    {
        var fallback = acceptanceCriteria
            .Select(ac => new AcResult(ac, AcStatus.NoEvidence, note))
            .ToList();

        return new SignoffResult(false, fallback);
    }

    private AiResponse? ParseResponse(string raw, int expectedCount)
    {
        var parsed = JsonSerializer.Deserialize<AiResponse>(raw, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (parsed?.Criteria == null || parsed.Criteria.Count == 0)
        {
            throw new JsonException("AI response did not include criteria results.");
        }

        parsed.Passed ??= parsed.Criteria.All(c => string.Equals(c.Status, "Met", StringComparison.OrdinalIgnoreCase));

        // Ensure indexes stay within range
        parsed.Criteria = parsed.Criteria
            .Where(c => c.Index >= 1 && c.Index <= expectedCount)
            .ToList();

        if (parsed.Criteria.Count == 0)
        {
            throw new JsonException("AI response did not map to known criteria indexes.");
        }

        return parsed;
    }

    private class AiResponse
    {
        [JsonPropertyName("passed")]
        public bool? Passed { get; set; }

        [JsonPropertyName("criteria")]
        public List<AiCriterion> Criteria { get; set; } = new();
    }

    private class AiCriterion
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;

        [JsonPropertyName("evidence")]
        public List<string>? Evidence { get; set; }
    }
}

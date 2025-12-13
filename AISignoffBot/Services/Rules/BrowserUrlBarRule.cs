using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;

namespace AISignoffBot.Services.Rules;

public class BrowserUrlBarRule(
    ILogger<BrowserUrlBarRule> logger,
    IAiClient aiClient) : ISignoffRule
{
    public async Task<RuleResult> EvaluateAsync(IReadOnlyList<EvidenceImage> evidenceImages, CancellationToken ct = default)
    {
        if (evidenceImages.Count == 0)
        {
            return new RuleResult(Array.Empty<RuleFailure>());
        }

        try
        {
            var response = await aiClient.GetVisionAnalysisAsync(
                BuildSystemPrompt(),
                BuildUserPrompt(evidenceImages),
                evidenceImages,
                ct);

            var parsed = ParseResponse(response);
            var failures = EvaluateFindings(parsed);

            return failures.Count == 0
                ? new RuleResult(Array.Empty<RuleFailure>())
                : new RuleResult([new RuleFailure(BuildFailureMessage(), failures)]);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Browser URL bar rule: AI returned invalid JSON");
            return new RuleResult(Array.Empty<RuleFailure>());
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Browser URL bar rule: AI request failed");
            return new RuleResult(Array.Empty<RuleFailure>());
        }
    }

    private static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are checking QA evidence screenshots for browser or web-app contexts and whether a URL/address bar is visible.");
        sb.AppendLine("Return JSON only.");

        return sb.ToString();
    }

    private static string BuildUserPrompt(IReadOnlyList<EvidenceImage> evidenceImages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("For each provided image, determine if it appears to be a browser/web-app interface and whether the URL/address bar is visible.");
        sb.AppendLine("Return strict JSON with this shape:");
        sb.AppendLine("{");
        sb.AppendLine("  \"screens\": [");
        sb.AppendLine("    { \"filename\": \"img.png\", \"browserLike\": true|false, \"urlBarVisible\": true|false }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("Use the exact filenames provided. Include every image once. If unsure, use false for boolean values.");
        sb.AppendLine();
        sb.AppendLine("Images:");

        foreach (var image in evidenceImages.OrderBy(i => i.Index))
        {
            sb.AppendLine($"- {image.Filename}: screenshot available");
        }

        return sb.ToString();
    }

    private static List<string> EvaluateFindings(BrowserUiResponse? response)
    {
        if (response?.Screens == null)
        {
            return [];
        }

        var flagged = new List<string>();

        foreach (var screen in response.Screens)
        {
            if (!screen.BrowserLike || screen.UrlBarVisible)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(screen.Filename))
            {
                flagged.Add(screen.Filename.Trim());
            }
        }

        return flagged.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildFailureMessage()
    {
        return "URL bar not visible; cannot confirm non-local environment.";
    }

    private static BrowserUiResponse? ParseResponse(string raw)
    {
        return JsonSerializer.Deserialize<BrowserUiResponse>(raw, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private class BrowserUiResponse
    {
        [JsonPropertyName("screens")]
        public List<BrowserUiFinding> Screens { get; set; } = new();
    }

    private class BrowserUiFinding
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("browserLike")]
        public bool BrowserLike { get; set; }
            = false;

        [JsonPropertyName("urlBarVisible")]
        public bool UrlBarVisible { get; set; }
            = false;
    }
}

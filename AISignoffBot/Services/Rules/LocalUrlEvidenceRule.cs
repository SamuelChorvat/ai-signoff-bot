using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;

namespace AISignoffBot.Services.Rules;

public class LocalUrlEvidenceRule(
    ILogger<LocalUrlEvidenceRule> logger,
    IAiClient aiClient) : ISignoffRule
{
    private static readonly Regex LocalhostLike = new(
        "^(?:(?:https?:\\/\\/)?(?:localhost|127\\.0\\.0\\.1|10\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}|192\\.168\\.\\d{1,3}\\.\\d{1,3}|172\\.(?:1[6-9]|2[0-9]|3[01])\\.\\d{1,3}\\.\\d{1,3}))(?:[:/].*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
                : new RuleResult([new RuleFailure(BuildFailureMessage(failures), failures)]);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Local URL rule: AI returned invalid JSON");
            return new RuleResult(Array.Empty<RuleFailure>());
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Local URL rule: AI request failed");
            return new RuleResult(Array.Empty<RuleFailure>());
        }
    }

    private static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are checking QA evidence screenshots for local or private URLs in the browser address bar.");
        sb.AppendLine("Return JSON only. If an address bar URL is not visible, omit it.");

        return sb.ToString();
    }

    private static string BuildUserPrompt(IReadOnlyList<EvidenceImage> evidenceImages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("For each provided image, read the browser/page URL visible in the address bar if present.");
        sb.AppendLine("Return strict JSON with this shape:");
        sb.AppendLine("{");
        sb.AppendLine("  \"screens\": [");
        sb.AppendLine("    { \"filename\": \"img.png\", \"url\": \"http://example.com/path\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("Omit images without a visible URL. Use the exact filename values given.");
        sb.AppendLine();
        sb.AppendLine("Images:");

        foreach (var image in evidenceImages.OrderBy(i => i.Index))
        {
            sb.AppendLine($"- {image.Filename}: screenshot available");
        }

        return sb.ToString();
    }

    private static List<string> EvaluateFindings(AddressBarResponse? response)
    {
        if (response?.Screens == null)
        {
            return [];
        }

        var flagged = new List<string>();

        foreach (var screen in response.Screens)
        {
            if (string.IsNullOrWhiteSpace(screen.Url))
            {
                continue;
            }

            var url = screen.Url.Trim();

            if (IsLocalUrl(url) && !string.IsNullOrWhiteSpace(screen.Filename))
            {
                flagged.Add(screen.Filename.Trim());
            }
        }

        return flagged.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildFailureMessage(IEnumerable<string> filenames)
    {
        return "Evidence shows a local/private URL (e.g., localhost). Provide screenshots from a shared environment.";
    }

    private static bool IsLocalUrl(string url)
    {
        if (LocalhostLike.IsMatch(url))
        {
            return true;
        }

        var normalized = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"http://{url}";

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (Uri.TryCreate(uri.GetLeftPart(UriPartial.Authority), UriKind.Absolute, out var authority))
        {
            if (IPAddress.TryParse(authority.Host, out var ip))
            {
                return IsPrivateIp(ip);
            }

            if (string.Equals(authority.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPrivateIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();

        // 10.0.0.0/8
        if (bytes[0] == 10)
        {
            return true;
        }

        // 172.16.0.0 – 172.31.255.255
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        return false;
    }

    private static AddressBarResponse? ParseResponse(string raw)
    {
        return JsonSerializer.Deserialize<AddressBarResponse>(raw, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private class AddressBarResponse
    {
        [JsonPropertyName("screens")]
        public List<AddressBarFinding> Screens { get; set; } = new();
    }

    private class AddressBarFinding
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}

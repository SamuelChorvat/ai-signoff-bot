using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.IO;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AISignoffBot.Services;

public class JiraClient : IJiraClient
{
    private readonly HttpClient _httpClient;
    private readonly JiraOptions _options;
    private readonly ILogger<JiraClient> _logger;

    private string? _flaggedFieldId;
    private readonly SemaphoreSlim _flaggedFieldLock = new(1, 1);

    private string? _botAccountId;
    private readonly SemaphoreSlim _botAccountLock = new(1, 1);

    // Cache evidence by issue and attachment cap to avoid repeated attachment list/download calls within the
    // process lifetime. Evidence is static for the same issue unless new attachments are added, and the
    // cache keeps repeated invocations of the evaluator from re-fetching identical data.
    private readonly ConcurrentDictionary<string, IReadOnlyList<JiraAttachmentEvidence>> _evidenceCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/webp"
    };


    public JiraClient(
        HttpClient httpClient,
        IOptions<JiraOptions> options,
        ILogger<JiraClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            string.IsNullOrWhiteSpace(_options.Email) ||
            string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            throw new ArgumentException("Jira configuration is incomplete.");
        }

        var authBytes = Encoding.ASCII.GetBytes($"{_options.Email}:{_options.ApiToken}");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IReadOnlyList<JiraAttachmentEvidence>> GetIssueEvidenceAsync(
        string issueKey,
        int maxEvidenceAttachments = 3,
        CancellationToken ct = default)
    {
        var cacheKey = GetEvidenceCacheKey(issueKey, maxEvidenceAttachments);

        if (maxEvidenceAttachments <= 0)
        {
            _logger.LogInformation("Max evidence attachments for {IssueKey} was {Max}. Returning no evidence.",
                issueKey, maxEvidenceAttachments);
            return CacheAndReturn(cacheKey, [JiraAttachmentEvidence.NoEvidence]);
        }

        if (_evidenceCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var url = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}?fields=attachment";

            _logger.LogInformation("Fetching attachments for {IssueKey}", issueKey);

            var response = await _httpClient.GetAsync(url, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Attachment fetch failed {StatusCode} for {IssueKey}. Body: {Body}",
                    response.StatusCode, issueKey, content);
                return CacheAndReturn(cacheKey, [JiraAttachmentEvidence.NoEvidence]);
            }

            using var doc = JsonDocument.Parse(content);
            var fields = doc.RootElement.GetProperty("fields");

            if (!fields.TryGetProperty("attachment", out var attachments) || attachments.ValueKind != JsonValueKind.Array)
            {
                _logger.LogInformation("No attachments found on {IssueKey}", issueKey);
                return CacheAndReturn(cacheKey, [JiraAttachmentEvidence.NoEvidence]);
            }

            var filtered = attachments
                .EnumerateArray()
                .Select(att => new
                {
                    Name = att.TryGetProperty("filename", out var n) ? n.GetString() : null,
                    Created = att.TryGetProperty("created", out var c) && DateTime.TryParse(c.GetString(), out var d)
                        ? d
                        : (DateTime?)null,
                    MimeType = att.TryGetProperty("mimeType", out var m) ? m.GetString() : null,
                    ContentUrl = att.TryGetProperty("content", out var u) ? u.GetString() : null
                })
                .Where(att => !string.IsNullOrWhiteSpace(att.Name)
                              && att.Created.HasValue
                              && !string.IsNullOrWhiteSpace(att.ContentUrl)
                              && IsImage(att.MimeType, att.Name!))
                .OrderByDescending(att => att.Created)
                .Take(maxEvidenceAttachments)
                .ToList();

            if (filtered.Count == 0)
            {
                _logger.LogInformation("No image attachments found on {IssueKey}", issueKey);
                return CacheAndReturn(cacheKey, [JiraAttachmentEvidence.NoEvidence]);
            }

            var evidence = new List<JiraAttachmentEvidence>();
            var index = 1;

            foreach (var att in filtered)
            {
                try
                {
                    var downloadResponse = await _httpClient.GetAsync(att.ContentUrl!, ct);
                    if (!downloadResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to download attachment {Name} for {IssueKey}: {Status}",
                            att.Name, issueKey, downloadResponse.StatusCode);
                        continue;
                    }

                    var bytes = await downloadResponse.Content.ReadAsByteArrayAsync(ct);

                    evidence.Add(new JiraAttachmentEvidence
                    {
                        Name = att.Name!,
                        Index = index++,
                        ContentType = att.MimeType,
                        DownloadUrl = att.ContentUrl,
                        ContentBytes = bytes
                    });
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    _logger.LogWarning(ex, "Error downloading attachment {Name} for {IssueKey}", att.Name, issueKey);
                }
            }

            if (evidence.Count == 0)
            {
                _logger.LogWarning("All attachment downloads failed for {IssueKey}", issueKey);
                return CacheAndReturn(cacheKey, [JiraAttachmentEvidence.NoEvidence]);
            }

            return CacheAndReturn(cacheKey, evidence);
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException)
        {
            _logger.LogWarning(ex, "Failed to retrieve attachments for {IssueKey}", issueKey);
            return CacheAndReturn(cacheKey, [JiraAttachmentEvidence.NoEvidence]);
        }
    }

    public async Task<JiraIssue> GetIssue(string issueKey, CancellationToken ct = default)
    {
        // Use v2; request only what we need
        var url = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}?fields=summary,description,labels";

        _logger.LogInformation("Fetching Jira issue {IssueKey} from {Url}", issueKey, url);

        var response = await _httpClient.GetAsync(url, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GetIssue failed {StatusCode} for {IssueKey}. Body: {Body}",
                response.StatusCode, issueKey, content);
            throw new HttpRequestException($"GetIssue failed {response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var key = root.GetProperty("key").GetString() ?? issueKey;
        var fields = root.GetProperty("fields");

        var summary = fields.TryGetProperty("summary", out var s) ? (s.GetString() ?? "") : "";
        var description = fields.TryGetProperty("description", out var d) ? (d.GetString() ?? "") : "";

        var labels = new List<string>();
        if (fields.TryGetProperty("labels", out var labelsEl) && labelsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var l in labelsEl.EnumerateArray())
            {
                var label = l.GetString();
                if (!string.IsNullOrWhiteSpace(label)) labels.Add(label);
            }
        }

        return new JiraIssue(key, summary, description, labels);
    }

    public async Task AddComment(string issueKey, string comment, CancellationToken ct = default)
    {
        // v2 accepts plain string comment body
        var url = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}/comment";
        var payload = new { body = comment };

        _logger.LogInformation("Adding comment to {IssueKey}", issueKey);

        var response = await _httpClient.PostAsJsonAsync(url, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);

            _logger.LogError(
                "AddComment failed {StatusCode} for {IssueKey}. Body: {Body}",
                response.StatusCode, issueKey, content);

            throw new HttpRequestException($"AddComment failed {response.StatusCode}: {content}");
        }
    }

    public async Task AddLabels(string issueKey, IEnumerable<string> labels, CancellationToken ct = default)
    {
        // Jira requires full label list, so we read existing labels, merge, then PUT.
        var issue = await GetIssue(issueKey, ct);

        var merged = new HashSet<string>(issue.Labels, StringComparer.OrdinalIgnoreCase);
        foreach (var l in labels)
        {
            if (!string.IsNullOrWhiteSpace(l)) merged.Add(l.Trim());
        }

        var url = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}";
        var payload = new
        {
            fields = new
            {
                labels = merged.ToArray()
            }
        };

        _logger.LogInformation("Updating labels for {IssueKey}: {Labels}", issueKey, string.Join(", ", merged));

        var response = await _httpClient.PutAsJsonAsync(url, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);

            _logger.LogError(
                "AddLabels failed {StatusCode} for {IssueKey}. Body: {Body}",
                response.StatusCode, issueKey, content);

            throw new HttpRequestException($"AddLabels failed {response.StatusCode}: {content}");
        }
    }

    public async Task TransitionToStatus(string issueKey, string targetStatusName, CancellationToken ct = default)
    {
        // Step 1: fetch available transitions
        var transitionsUrl = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}/transitions";

        _logger.LogInformation("Fetching transitions for {IssueKey}", issueKey);

        var transResp = await _httpClient.GetAsync(transitionsUrl, ct);
        var transBody = await transResp.Content.ReadAsStringAsync(ct);

        if (!transResp.IsSuccessStatusCode)
        {
            _logger.LogError("GetTransitions failed {StatusCode} for {IssueKey}. Body: {Body}",
                transResp.StatusCode, issueKey, transBody);
            throw new HttpRequestException($"GetTransitions failed {transResp.StatusCode}: {transBody}");
        }

        using var doc = JsonDocument.Parse(transBody);
        var transitions = doc.RootElement.GetProperty("transitions");

        string? transitionId = null;

        foreach (var t in transitions.EnumerateArray())
        {
            // Jira transitions have "name" and "id"
            var name = t.GetProperty("name").GetString();
            if (string.Equals(name, targetStatusName, StringComparison.OrdinalIgnoreCase))
            {
                transitionId = t.GetProperty("id").GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(transitionId))
        {
            _logger.LogWarning("No transition named '{Target}' found for {IssueKey}. Available: {Available}",
                targetStatusName, issueKey,
                string.Join(", ", transitions.EnumerateArray().Select(t => t.GetProperty("name").GetString())));

            throw new InvalidOperationException(
                $"No transition named '{targetStatusName}' found for issue {issueKey}.");
        }

        // Step 2: perform transition
        var doTransitionUrl = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}/transitions";
        var payload = new
        {
            transition = new { id = transitionId }
        };

        _logger.LogInformation("Transitioning {IssueKey} using transition '{Target}' (id={Id})",
            issueKey, targetStatusName, transitionId);

        var resp = await _httpClient.PostAsJsonAsync(doTransitionUrl, payload, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Transition failed {StatusCode} for {IssueKey}. Body: {Body}",
                resp.StatusCode, issueKey, body);

            throw new HttpRequestException($"Transition failed {resp.StatusCode}: {body}");
        }
    }
    
    public async Task SetFlagged(string issueKey, bool flagged, CancellationToken ct = default)
    {
        var flaggedFieldId = await GetFlaggedFieldId(ct);

        var url = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}";

        // Jira expects an array of objects like [{ "value": "Impediment" }] when flagged.
        // Clearing is empty array.
        object value = flagged
            ? new[] { new Dictionary<string, string> { ["value"] = "Impediment" } }
            : Array.Empty<object>();

        var payload = new
        {
            fields = new Dictionary<string, object>
            {
                [flaggedFieldId] = value
            }
        };

        _logger.LogInformation("Setting Flagged={Flagged} for {IssueKey} using {FieldId}",
            flagged, issueKey, flaggedFieldId);

        var response = await _httpClient.PutAsJsonAsync(url, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("SetFlagged failed {StatusCode} for {IssueKey}. Body: {Body}",
                response.StatusCode, issueKey, body);
            throw new HttpRequestException($"SetFlagged failed {response.StatusCode}: {body}");
        }
    }
    
    public async Task SetLabels(string issueKey, IEnumerable<string> labels, CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}";

        var cleaned = labels
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var payload = new
        {
            fields = new
            {
                labels = cleaned
            }
        };

        _logger.LogInformation("Setting labels for {IssueKey}: {Labels}", issueKey, string.Join(", ", cleaned));

        var response = await _httpClient.PutAsJsonAsync(url, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("SetLabels failed {StatusCode} for {IssueKey}. Body: {Body}", response.StatusCode, issueKey, body);
            throw new HttpRequestException($"SetLabels failed {response.StatusCode}: {body}");
        }
    }
    
    public async Task Assign(string issueKey, string accountId, CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}/assignee";
        var payload = new { accountId };

        _logger.LogInformation("Assigning {IssueKey} to accountId {AccountId}", issueKey, accountId);

        var response = await _httpClient.PutAsJsonAsync(url, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Assign failed {StatusCode} for {IssueKey}. Body: {Body}", response.StatusCode, issueKey, body);
            throw new HttpRequestException($"Assign failed {response.StatusCode}: {body}");
        }
    }

    
    public async Task AssignToBot(string issueKey, CancellationToken ct = default)
    {
        var accountId = await GetBotAccountId(ct);
        await Assign(issueKey, accountId, ct);
    }

    private static string GetEvidenceCacheKey(string issueKey, int maxEvidenceAttachments)
        => $"{issueKey}|{maxEvidenceAttachments}";

    private IReadOnlyList<JiraAttachmentEvidence> CacheAndReturn(string cacheKey, IReadOnlyList<JiraAttachmentEvidence> evidence)
    {
        _evidenceCache[cacheKey] = evidence;
        return evidence;
    }

    private static bool IsImage(string? mimeType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(mimeType) && AllowedImageTypes.Contains(mimeType))
            return true;

        var ext = Path.GetExtension(fileName);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetFlaggedFieldId(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_flaggedFieldId))
            return _flaggedFieldId!;

        await _flaggedFieldLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_flaggedFieldId))
                return _flaggedFieldId!;

            var url = $"{_options.BaseUrl}/rest/api/2/field";
            _logger.LogInformation("Discovering Jira field id for 'Flagged' via {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Field discovery failed {StatusCode}. Body: {Body}", response.StatusCode, body);
                throw new HttpRequestException($"Field discovery failed {response.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);

            foreach (var field in doc.RootElement.EnumerateArray())
            {
                var name = field.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.Equals(name, "Flagged", StringComparison.OrdinalIgnoreCase))
                {
                    var id = field.GetProperty("id").GetString();
                    if (string.IsNullOrWhiteSpace(id))
                        break;

                    _flaggedFieldId = id;
                    _logger.LogInformation("Discovered Flagged field id: {FieldId}", _flaggedFieldId);
                    return _flaggedFieldId!;
                }
            }

            throw new InvalidOperationException("Could not find Jira field named 'Flagged'. Is it enabled for this project?");
        }
        finally
        {
            _flaggedFieldLock.Release();
        }
    }
    
    private async Task<string> GetBotAccountId(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_botAccountId))
            return _botAccountId!;

        await _botAccountLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_botAccountId))
                return _botAccountId!;

            var url = $"{_options.BaseUrl}/rest/api/2/user/search?query={Uri.EscapeDataString(_options.Email)}";

            _logger.LogInformation("Resolving bot accountId for {Email}", _options.Email);

            var response = await _httpClient.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"User lookup failed {response.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);

            var user = doc.RootElement.EnumerateArray().FirstOrDefault();
            var accountId = user.GetProperty("accountId").GetString();

            if (string.IsNullOrWhiteSpace(accountId))
                throw new InvalidOperationException($"Could not resolve accountId for {_options.Email}");

            _botAccountId = accountId;

            _logger.LogInformation("Resolved bot accountId: {AccountId}", _botAccountId);

            return _botAccountId!;
        }
        finally
        {
            _botAccountLock.Release();
        }
    }
}

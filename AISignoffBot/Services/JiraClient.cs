using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

    public async Task<JiraIssue> GetIssue(string issueKey, CancellationToken ct = default)
    {
        // Use v2; request only what we need
        var url = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}?fields=summary,description,labels,attachment";

        _logger.LogInformation("Fetching Jira issue {IssueKey} from {Url}", issueKey, url);

        var response = await _httpClient.GetAsync(url, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GetIssue failed {StatusCode} for {IssueKey}. Body: {Body}",
                response.StatusCode, issueKey, responseContent);
            throw new HttpRequestException($"GetIssue failed {response.StatusCode}: {responseContent}");
        }

        using var doc = JsonDocument.Parse(responseContent);
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

        var attachments = new List<JiraAttachment>();
        if (fields.TryGetProperty("attachment", out var attachmentsEl) && attachmentsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var attachmentEl in attachmentsEl.EnumerateArray())
            {
                var filename = attachmentEl.TryGetProperty("filename", out var fn) ? (fn.GetString() ?? string.Empty) : string.Empty;
                var mimeType = attachmentEl.TryGetProperty("mimeType", out var mt) ? (mt.GetString() ?? string.Empty) : string.Empty;
                var contentUrl = attachmentEl.TryGetProperty("content", out var contentProperty) ? (contentProperty.GetString() ?? string.Empty) : string.Empty;

                attachments.Add(new JiraAttachment(filename, mimeType, contentUrl));
            }
        }

        _logger.LogInformation("Retrieved {AttachmentCount} attachments for {IssueKey}", attachments.Count, key);

        return new JiraIssue(key, summary, description, labels, attachments);
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

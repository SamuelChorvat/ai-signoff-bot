using System.Net.Http.Headers;
using System.Text;
using AISignoffBot.Models;
using Microsoft.Extensions.Options;

namespace AISignoffBot.Services;

public class JiraClient : IJiraClient
{
    private readonly HttpClient _httpClient;
    private readonly JiraOptions _options;
    private readonly ILogger<JiraClient> _logger;

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
    }

    public async Task AddComment(string issueKey, string comment)
    {
        var url = $"{_options.BaseUrl}/rest/api/2/issue/{issueKey}/comment";
        var payload = new { body = comment };

        _logger.LogInformation("Sending Jira comment to {IssueKey} at {Url}", issueKey, url);

        var response = await _httpClient.PostAsJsonAsync(url, payload);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogError(
                "Jira API returned {StatusCode} when commenting on {IssueKey}. Response body: {Content}",
                response.StatusCode, issueKey, content);

            throw new HttpRequestException(
                $"Jira API returned {response.StatusCode}: {content}");
        }

        _logger.LogInformation("Successfully added Jira comment to issue {IssueKey}", issueKey);
    }
}
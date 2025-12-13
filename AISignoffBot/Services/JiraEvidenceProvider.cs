using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AISignoffBot.Services;

public class JiraEvidenceProvider(
    IJiraClient jiraClient,
    IOptions<JiraOptions> options,
    ILogger<JiraEvidenceProvider> logger) : IEvidenceProvider
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    private readonly IJiraClient _jiraClient = jiraClient;
    private readonly ILogger<JiraEvidenceProvider> _logger = logger;
    private readonly JiraOptions _options = options.Value;

    public async Task<IReadOnlyList<EvidenceImage>> GetLatestImagesAsync(string issueKey, CancellationToken ct = default)
    {
        var issue = await _jiraClient.GetIssue(issueKey, ct);

        var imageAttachments = issue.Attachments
            .Where(a => AllowedMimeTypes.Contains(a.MimeType))
            .OrderByDescending(a => a.Created)
            .Take(_options.MaxEvidenceImages)
            .ToList();

        _logger.LogInformation(
            "Found {Count} image attachments for {IssueKey} (limit {Limit})",
            imageAttachments.Count,
            issueKey,
            _options.MaxEvidenceImages);

        var evidenceImages = new List<EvidenceImage>(imageAttachments.Count);

        for (var i = 0; i < imageAttachments.Count; i++)
        {
            var attachment = imageAttachments[i];

            _logger.LogInformation(
                "Downloading attachment {Filename} ({MimeType}) for {IssueKey}",
                attachment.Filename,
                attachment.MimeType,
                issueKey);

            var bytes = await _jiraClient.DownloadAttachmentAsync(attachment.ContentUrl, ct);

            _logger.LogInformation(
                "Downloaded attachment {Filename} ({MimeType}) with {ByteCount} bytes for {IssueKey}",
                attachment.Filename,
                attachment.MimeType,
                bytes.Length,
                issueKey);

            evidenceImages.Add(new EvidenceImage(attachment.Filename, attachment.MimeType, bytes, i));
        }

        return evidenceImages;
    }
}

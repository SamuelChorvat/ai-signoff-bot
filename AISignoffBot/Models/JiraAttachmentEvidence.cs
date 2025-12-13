namespace AISignoffBot.Models;

public record JiraAttachmentEvidence
{
    public required string Name { get; init; }
    public required int Index { get; init; }
    public string? ContentType { get; init; }
    public string? DownloadUrl { get; init; }
    public byte[]? ContentBytes { get; init; }

    public static JiraAttachmentEvidence NoEvidence { get; } = new()
    {
        Name = "No evidence attached.",
        Index = 1,
        ContentType = null,
        DownloadUrl = null,
        ContentBytes = null
    };
}

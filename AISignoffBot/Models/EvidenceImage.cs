namespace AISignoffBot.Models;

public record EvidenceImage(string Filename, string MimeType, string AttachmentUrl, byte[] Bytes, int Index)
{
    public string EvidenceKey => $"img{Index + 1}";
}

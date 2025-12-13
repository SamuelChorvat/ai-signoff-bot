namespace AISignoffBot.Models;

public record EvidenceImage(string Filename, string MimeType, byte[] Bytes, int Index);

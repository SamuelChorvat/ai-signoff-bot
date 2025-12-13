namespace AISignoffBot.Models;

public record RuleFailure(string Message, IReadOnlyList<string> EvidenceFilenames);

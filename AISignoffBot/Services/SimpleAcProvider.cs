using AISignoffBot.Services.Interfaces;

namespace AISignoffBot.Services;

public class SimpleAcProvider : IAcProvider
{
    // Looks for lines like:
    // - AC1: something
    // - something
    public IReadOnlyList<string> ExtractAcceptanceCriteria(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return [];

        // Minimal: take bullet lines after a heading "Acceptance Criteria:"
        var idx = description.IndexOf("Acceptance Criteria", StringComparison.OrdinalIgnoreCase);
        var text = idx >= 0 ? description[idx..] : description;

        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("-") || l.StartsWith("*"))
            .Select(l => l.TrimStart('-', '*', ' ').Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        return lines;
    }
}

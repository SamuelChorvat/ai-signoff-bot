namespace AISignoffBot.Services.Interfaces;

public interface IAcProvider
{
    IReadOnlyList<string> ExtractAcceptanceCriteria(string? description);
}
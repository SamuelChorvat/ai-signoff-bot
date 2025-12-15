namespace AISignoffBot.Models;

public class AiOptions
{
    public string Provider { get; init; } = "OpenAI";

    public string Model { get; init; } = "gpt-4o";

    public string ApiKey { get; init; } = string.Empty;
}

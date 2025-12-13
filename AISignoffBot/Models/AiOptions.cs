namespace AISignoffBot.Models;

public class AiOptions
{
    public string Provider { get; set; } = "OpenAI";

    public string Model { get; set; } = "gpt-4o";

    public string ApiKey { get; set; } = string.Empty;
}

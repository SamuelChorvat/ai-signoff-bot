namespace AISignoffBot.Models;

public class CommentFooterOptions
{
    public string BotName { get; init; } = "AI Signoff Bot";
    public string BuildShaEnvironmentVariable { get; init; } = "BUILD_SHA";
}

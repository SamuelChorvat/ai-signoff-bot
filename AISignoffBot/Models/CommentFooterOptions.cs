namespace AISignoffBot.Models;

public class CommentFooterOptions
{
    public string BotName { get; set; } = "AI Signoff Bot";
    public string BuildShaEnvironmentVariable { get; set; } = "BUILD_SHA";
}

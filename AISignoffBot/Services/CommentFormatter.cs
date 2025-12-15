using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AISignoffBot.Services;

public class CommentFormatter : ICommentFormatter
{
    private readonly CommentFooterOptions options;
    private readonly IBuildInfoProvider buildInfoProvider;

    public CommentFormatter(IOptions<CommentFooterOptions> options, IBuildInfoProvider buildInfoProvider)
    {
        this.options = options.Value;
        this.buildInfoProvider = buildInfoProvider;
    }

    public string WithFooter(string content)
    {
        var cleanBody = (content ?? string.Empty).TrimEnd();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'");
        var footer = $"{options.BotName} • build {buildInfoProvider.GetShortSha()} • {timestamp}";
        return $"{cleanBody}\n—\n{footer}";
    }
}

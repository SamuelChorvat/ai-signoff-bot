using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AISignoffBot.Services;

public class CommentFormatter(IOptions<CommentFooterOptions> options, IBuildInfoProvider buildInfoProvider)
    : ICommentFormatter
{
    private readonly CommentFooterOptions _options = options.Value;

    public string WithFooter(string? content)
    {
        var cleanBody = (content ?? string.Empty).TrimEnd();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'");
        var footer = $"🤖 {_options.BotName} • build {buildInfoProvider.GetShortSha()} • {timestamp}";

        return $"{cleanBody}\n\n{{panel:bgColor=#DEEBFF}}\n{footer}\n{{panel}}";
    }
}

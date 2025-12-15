using System.Reflection;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AISignoffBot.Services;

public class BuildInfoProvider : IBuildInfoProvider
{
    private readonly CommentFooterOptions options;
    private readonly string resolvedSha;

    public BuildInfoProvider(IOptions<CommentFooterOptions> options)
    {
        this.options = options.Value;
        resolvedSha = ResolveSha();
    }

    public string GetShortSha()
    {
        return resolvedSha.Length > 7 ? resolvedSha[..7] : resolvedSha;
    }

    private string ResolveSha()
    {
        var envSha = Environment.GetEnvironmentVariable(options.BuildShaEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envSha))
        {
            return Normalize(envSha);
        }

        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var assemblySha = ExtractSha(informational);
        if (!string.IsNullOrWhiteSpace(assemblySha))
        {
            return Normalize(assemblySha);
        }

        return "dev";
    }

    private static string Normalize(string sha)
    {
        var trimmed = sha.Trim();
        return trimmed.Length > 40 ? trimmed[..40] : trimmed;
    }

    private static string? ExtractSha(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (LooksLikeSha(value))
        {
            return value;
        }

        foreach (var segment in value.Split(['+', '-', '.', ' ', '_'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (LooksLikeSha(segment))
            {
                return segment;
            }
        }

        return null;
    }

    private static bool LooksLikeSha(string value)
    {
        if (value.Length < 7 || value.Length > 40)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}

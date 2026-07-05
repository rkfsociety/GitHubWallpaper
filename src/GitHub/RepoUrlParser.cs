using System.Text.RegularExpressions;

namespace GitHubWallpaper.GitHub;

/// <summary>
/// Разбирает строки вида <c>owner/repo</c> и <c>https://github.com/owner/repo</c>.
/// </summary>
internal static partial class RepoUrlParser
{
    private static readonly Regex NamePattern = ValidNameRegex();

    /// <summary>
    /// Пытается разобрать строку в <see cref="RepoReference"/>.
    /// </summary>
    public static bool TryParse(string? input, out RepoReference reference)
    {
        reference = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();

        if (TryParseFromUri(trimmed, out reference))
        {
            return true;
        }

        if (TryParseFromSlug(trimmed, out reference))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Разбирает строку в <see cref="RepoReference"/> или выбрасывает <see cref="FormatException"/>.
    /// </summary>
    public static RepoReference Parse(string input)
    {
        if (TryParse(input, out var reference))
        {
            return reference;
        }

        throw new FormatException(
            "Ожидается формат owner/repo или https://github.com/owner/repo.");
    }

    private static bool TryParseFromUri(string input, out RepoReference reference)
    {
        reference = default;

        var candidate = input.Contains("://", StringComparison.Ordinal)
            ? input
            : input.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase)
                || input.StartsWith("www.github.com/", StringComparison.OrdinalIgnoreCase)
                ? $"https://{input.TrimStart('/')}"
                : null;

        if (candidate is null || !Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!IsGitHubHost(uri.Host))
        {
            return false;
        }

        return TryCreateFromPath(uri.AbsolutePath, out reference);
    }

    private static bool TryParseFromSlug(string input, out RepoReference reference)
    {
        reference = default;

        var path = input;

        var hashIndex = path.IndexOf('#');
        if (hashIndex >= 0)
        {
            path = path[..hashIndex];
        }

        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        return TryCreateFromPath(path, out reference);
    }

    private static bool TryCreateFromPath(string path, out RepoReference reference)
    {
        reference = default;

        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 2)
        {
            return false;
        }

        var owner = segments[0];
        var repo = segments[1];

        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repo = repo[..^4];
        }

        if (!IsValidName(owner) || !IsValidName(repo))
        {
            return false;
        }

        reference = new RepoReference(owner, repo);
        return true;
    }

    private static bool IsGitHubHost(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidName(string name) =>
        !string.IsNullOrEmpty(name) && NamePattern.IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex ValidNameRegex();
}

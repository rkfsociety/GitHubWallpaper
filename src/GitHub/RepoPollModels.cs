using System.Text.Json;

namespace GitHubWallpaper.GitHub;

/// <summary>Снимок метаданных репозитория из <c>GET /repos/{owner}/{repo}</c>.</summary>
internal sealed record RepoMetadataSnapshot(
    RepoReference Repository,
    string FullName,
    string? Description,
    int StargazersCount,
    int ForksCount,
    int OpenIssuesCount,
    string HtmlUrl,
    DateTimeOffset FetchedAt);

/// <summary>Один коммит из <c>GET /repos/{owner}/{repo}/commits</c>.</summary>
internal sealed record RepoCommitSnapshot(
    string Sha,
    string Message,
    string? AuthorName,
    DateTimeOffset? AuthorDate,
    string HtmlUrl);

/// <summary>Аргументы события успешного опроса.</summary>
internal sealed class RepoPollUpdatedEventArgs<T> : EventArgs
{
    public RepoPollUpdatedEventArgs(RepoReference repository, T data)
    {
        Repository = repository;
        Data = data;
    }

    public RepoReference Repository { get; }

    public T Data { get; }
}

/// <summary>Аргументы события ошибки опроса.</summary>
internal sealed class RepoPollFailedEventArgs : EventArgs
{
    public RepoPollFailedEventArgs(RepoReference repository, RepoPollKind kind, Exception exception)
    {
        Repository = repository;
        Kind = kind;
        Exception = exception;
    }

    public RepoReference Repository { get; }

    public RepoPollKind Kind { get; }

    public Exception Exception { get; }
}

internal enum RepoPollKind
{
    Metadata,
    Commits,
}

internal static class RepoApiParser
{
    public static RepoMetadataSnapshot ParseMetadata(RepoReference repository, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new RepoMetadataSnapshot(
            repository,
            ReadString(root, "full_name") ?? repository.Slug,
            ReadOptionalString(root, "description"),
            ReadInt(root, "stargazers_count"),
            ReadInt(root, "forks_count"),
            ReadInt(root, "open_issues_count"),
            ReadString(root, "html_url") ?? repository.HtmlUrl.ToString(),
            DateTimeOffset.UtcNow);
    }

    public static IReadOnlyList<RepoCommitSnapshot> ParseCommits(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var commits = new List<RepoCommitSnapshot>();

        foreach (var item in root.EnumerateArray())
        {
            var commit = item.GetProperty("commit");
            var message = ReadString(commit, "message") ?? string.Empty;
            var firstLine = message.Split('\n', '\r')[0];

            string? authorName = null;
            DateTimeOffset? authorDate = null;

            if (commit.TryGetProperty("author", out var author))
            {
                authorName = ReadOptionalString(author, "name");
                authorDate = ReadOptionalDate(author, "date");
            }

            commits.Add(new RepoCommitSnapshot(
                ReadString(item, "sha") ?? string.Empty,
                firstLine,
                authorName,
                authorDate,
                ReadString(item, "html_url") ?? string.Empty));
        }

        return commits;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var number)
            ? number
            : 0;

    private static DateTimeOffset? ReadOptionalDate(JsonElement element, string propertyName)
    {
        var text = ReadOptionalString(element, propertyName);
        return DateTimeOffset.TryParse(text, out var date) ? date : null;
    }
}

using System.Text.Json;

namespace GitHubWallpaper.GitHub;

/// <summary>Открытый pull request.</summary>
internal sealed record RepoPullSnapshot(
    int Number,
    string Title,
    string? UserLogin,
    DateTimeOffset? CreatedAt,
    string HtmlUrl);

/// <summary>Открытый issue (без pull request).</summary>
internal sealed record RepoIssueSnapshot(
    int Number,
    string Title,
    string? UserLogin,
    DateTimeOffset? CreatedAt,
    string HtmlUrl);

/// <summary>Релиз репозитория.</summary>
internal sealed record RepoReleaseSnapshot(
    long Id,
    string TagName,
    string Name,
    bool IsPrerelease,
    DateTimeOffset? PublishedAt,
    string HtmlUrl);

/// <summary>Последний workflow run GitHub Actions.</summary>
internal sealed record RepoCiRunSnapshot(
    long Id,
    string Name,
    string Status,
    string? Conclusion,
    DateTimeOffset? UpdatedAt,
    string HtmlUrl);

/// <summary>Одна неделя commit activity (7 дней).</summary>
internal sealed record HeatmapWeekSnapshot(
    int Total,
    IReadOnlyList<int> Days);

/// <summary>52 недели commit activity.</summary>
internal sealed record RepoHeatmapSnapshot(
    IReadOnlyList<HeatmapWeekSnapshot> Weeks,
    DateTimeOffset FetchedAt);

/// <summary>Событие из <c>GET /repos/{owner}/{repo}/events</c>.</summary>
internal sealed record RepoEventSnapshot(
    string Id,
    string Type,
    string? ActorLogin,
    DateTimeOffset? CreatedAt,
    string Summary,
    string HtmlUrl);

/// <summary>Элемент объединённой ленты активности.</summary>
internal sealed record ActivityFeedItem(
    string Id,
    string Kind,
    string Title,
    string? Subtitle,
    DateTimeOffset Timestamp,
    string HtmlUrl,
    bool IsNew);

internal static class ActivityApiParser
{
    public static IReadOnlyList<RepoPullSnapshot> ParsePulls(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var pulls = new List<RepoPullSnapshot>();

        foreach (var item in root.EnumerateArray())
        {
            pulls.Add(new RepoPullSnapshot(
                ReadInt(item, "number"),
                ReadString(item, "title") ?? string.Empty,
                ReadNestedString(item, "user", "login"),
                ReadOptionalDate(item, "created_at"),
                ReadString(item, "html_url") ?? string.Empty));
        }

        return pulls;
    }

    public static IReadOnlyList<RepoIssueSnapshot> ParseIssues(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var issues = new List<RepoIssueSnapshot>();

        foreach (var item in root.EnumerateArray())
        {
            if (item.TryGetProperty("pull_request", out _))
            {
                continue;
            }

            issues.Add(new RepoIssueSnapshot(
                ReadInt(item, "number"),
                ReadString(item, "title") ?? string.Empty,
                ReadNestedString(item, "user", "login"),
                ReadOptionalDate(item, "created_at"),
                ReadString(item, "html_url") ?? string.Empty));
        }

        return issues;
    }

    public static IReadOnlyList<RepoReleaseSnapshot> ParseReleases(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var releases = new List<RepoReleaseSnapshot>();

        foreach (var item in root.EnumerateArray())
        {
            releases.Add(new RepoReleaseSnapshot(
                ReadLong(item, "id"),
                ReadString(item, "tag_name") ?? string.Empty,
                ReadString(item, "name") ?? ReadString(item, "tag_name") ?? string.Empty,
                ReadBool(item, "prerelease"),
                ReadOptionalDate(item, "published_at"),
                ReadString(item, "html_url") ?? string.Empty));
        }

        return releases;
    }

    public static RepoCiRunSnapshot? ParseLatestCiRun(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("workflow_runs", out var runs)
            || runs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var first = runs.EnumerateArray().FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return new RepoCiRunSnapshot(
            ReadLong(first, "id"),
            ReadString(first, "name") ?? "Workflow",
            ReadString(first, "status") ?? "unknown",
            ReadOptionalString(first, "conclusion"),
            ReadOptionalDate(first, "updated_at"),
            ReadString(first, "html_url") ?? string.Empty);
    }

    public static RepoHeatmapSnapshot ParseHeatmap(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            return new RepoHeatmapSnapshot([], DateTimeOffset.UtcNow);
        }

        var weeks = new List<HeatmapWeekSnapshot>();

        foreach (var week in root.EnumerateArray())
        {
            var days = new List<int>();

            if (week.TryGetProperty("days", out var daysElement)
                && daysElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var day in daysElement.EnumerateArray())
                {
                    days.Add(day.TryGetInt32(out var count) ? count : 0);
                }
            }

            while (days.Count < 7)
            {
                days.Add(0);
            }

            weeks.Add(new HeatmapWeekSnapshot(
                ReadInt(week, "total"),
                days));
        }

        return new RepoHeatmapSnapshot(weeks, DateTimeOffset.UtcNow);
    }

    public static IReadOnlyList<RepoEventSnapshot> ParseEvents(string json, RepoReference repository)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var events = new List<RepoEventSnapshot>();

        foreach (var item in root.EnumerateArray())
        {
            var type = ReadString(item, "type") ?? "Event";
            var actor = ReadNestedString(item, "actor", "login");
            var createdAt = ReadOptionalDate(item, "created_at");
            var id = ReadString(item, "id") ?? $"{type}-{createdAt?.ToUnixTimeSeconds()}";

            events.Add(new RepoEventSnapshot(
                id,
                type,
                actor,
                createdAt,
                BuildEventSummary(type, actor, item),
                repository.HtmlUrl.ToString()));
        }

        return events;
    }

    private static string BuildEventSummary(string type, string? actor, JsonElement item)
    {
        var login = actor ?? "someone";

        return type switch
        {
            "PushEvent" => $"{login} pushed commits",
            "PullRequestEvent" => $"{login} {ReadNestedString(item, "payload", "action") ?? "updated"} a pull request",
            "IssuesEvent" => $"{login} {ReadNestedString(item, "payload", "action") ?? "updated"} an issue",
            "ReleaseEvent" => $"{login} published a release",
            "CreateEvent" => $"{login} created {ReadNestedString(item, "payload", "ref_type") ?? "resource"}",
            "WatchEvent" => $"{login} starred the repository",
            "ForkEvent" => $"{login} forked the repository",
            _ => $"{login}: {type}",
        };
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

    private static string? ReadNestedString(JsonElement element, string objectName, string propertyName)
    {
        if (!element.TryGetProperty(objectName, out var nested))
        {
            return null;
        }

        return ReadString(nested, propertyName);
    }

    private static int ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var number)
            ? number
            : 0;

    private static long ReadLong(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var number)
            ? number
            : 0;

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
        && value.GetBoolean();

    private static DateTimeOffset? ReadOptionalDate(JsonElement element, string propertyName)
    {
        var text = ReadOptionalString(element, propertyName);
        return DateTimeOffset.TryParse(text, out var date) ? date : null;
    }
}

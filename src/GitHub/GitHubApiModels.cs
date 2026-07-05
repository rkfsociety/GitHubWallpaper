using System.Net;
using System.Net.Http.Headers;

namespace GitHubWallpaper.GitHub;

/// <summary>
/// Снимок заголовков <c>X-RateLimit-*</c> из ответа GitHub API.
/// </summary>
internal readonly record struct GitHubRateLimit(
    int? Limit,
    int? Remaining,
    int? Used,
    DateTimeOffset? ResetAt)
{
    public static GitHubRateLimit TryParse(HttpResponseMessage response)
    {
        var headers = response.Headers;

        return new GitHubRateLimit(
            ParseInt(headers, "X-RateLimit-Limit"),
            ParseInt(headers, "X-RateLimit-Remaining"),
            ParseInt(headers, "X-RateLimit-Used"),
            ParseResetAt(headers));
    }

    private static int? ParseInt(HttpHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values))
        {
            return null;
        }

        return int.TryParse(values.FirstOrDefault(), out var value) ? value : null;
    }

    private static DateTimeOffset? ParseResetAt(HttpHeaders headers)
    {
        if (!headers.TryGetValues("X-RateLimit-Reset", out var values))
        {
            return null;
        }

        if (!long.TryParse(values.FirstOrDefault(), out var unixSeconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }
}

/// <summary>
/// Успешный ответ GitHub REST API.
/// </summary>
internal sealed record GitHubApiResult(
    HttpStatusCode StatusCode,
    string Body,
    GitHubRateLimit RateLimit,
    string? ETag,
    bool IsNotModified = false);

/// <summary>
/// Ошибка запроса к GitHub REST API.
/// </summary>
internal sealed class GitHubApiException : Exception
{
    public GitHubApiException(
        HttpStatusCode statusCode,
        string path,
        string? responseBody,
        GitHubRateLimit rateLimit)
        : base(BuildMessage(statusCode, path, responseBody))
    {
        StatusCode = statusCode;
        Path = path;
        ResponseBody = responseBody;
        RateLimit = rateLimit;
    }

    public HttpStatusCode StatusCode { get; }

    public string Path { get; }

    public string? ResponseBody { get; }

    public GitHubRateLimit RateLimit { get; }

    private static string BuildMessage(HttpStatusCode statusCode, string path, string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return $"GitHub API вернул {(int)statusCode} для {path}.";
        }

        return $"GitHub API вернул {(int)statusCode} для {path}: {responseBody}";
    }
}

using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace GitHubWallpaper.GitHub;

/// <summary>Код ошибки опроса GitHub для UI и логики повторов.</summary>
internal enum GitHubPollErrorCode
{
    NotFound,
    Forbidden,
    Unauthorized,
    RateLimited,
    Network,
    ServerError,
    Unknown,
}

/// <summary>Пользовательское описание ошибки опроса репозитория.</summary>
internal sealed record GitHubPollError(
    GitHubPollErrorCode Code,
    string Message,
    string? Hint,
    bool IsFatalForRepo)
{
    public string CodeName => Code switch
    {
        GitHubPollErrorCode.NotFound => "not_found",
        GitHubPollErrorCode.Forbidden => "forbidden",
        GitHubPollErrorCode.Unauthorized => "unauthorized",
        GitHubPollErrorCode.RateLimited => "rate_limited",
        GitHubPollErrorCode.Network => "network",
        GitHubPollErrorCode.ServerError => "server_error",
        _ => "unknown",
    };

    public static GitHubPollError FromException(Exception exception)
    {
        return exception switch
        {
            GitHubApiException api => FromApiException(api),
            HttpRequestException http => new(
                GitHubPollErrorCode.Network,
                "Не удалось связаться с GitHub.",
                "Проверьте подключение к интернету. Повторим при следующем опросе.",
                IsFatalForRepo: false),
            TaskCanceledException { InnerException: TimeoutException }
                or TaskCanceledException when exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) =>
                new(
                    GitHubPollErrorCode.Network,
                    "Превышено время ожидания ответа GitHub.",
                    "Повторим при следующем опросе.",
                    IsFatalForRepo: false),
            OperationCanceledException => new(
                GitHubPollErrorCode.Unknown,
                "Опрос отменён.",
                null,
                IsFatalForRepo: false),
            _ => new(
                GitHubPollErrorCode.Unknown,
                exception.Message,
                null,
                IsFatalForRepo: false),
        };
    }

    private static GitHubPollError FromApiException(GitHubApiException api)
    {
        return api.StatusCode switch
        {
            HttpStatusCode.NotFound => new(
                GitHubPollErrorCode.NotFound,
                $"Репозиторий не найден: {ExtractRepoPath(api.Path)}.",
                "Проверьте owner/repo в настройках. Для приватных репозиториев нужен PAT со scope repo.",
                IsFatalForRepo: true),

            HttpStatusCode.Unauthorized => new(
                GitHubPollErrorCode.Unauthorized,
                "Токен GitHub недействителен или истёк.",
                "Откройте Настройки и обновите PAT.",
                IsFatalForRepo: false),

            HttpStatusCode.Forbidden when IsRateLimitBody(api.ResponseBody) => new(
                GitHubPollErrorCode.RateLimited,
                "Исчерпан лимит запросов GitHub API.",
                "Добавьте PAT или дождитесь сброса лимита.",
                IsFatalForRepo: false),

            HttpStatusCode.Forbidden => new(
                GitHubPollErrorCode.Forbidden,
                $"Нет доступа к репозиторию: {ExtractRepoPath(api.Path)}.",
                "Для приватных репозиториев создайте PAT со scope repo.",
                IsFatalForRepo: true),

            HttpStatusCode.TooManyRequests => new(
                GitHubPollErrorCode.RateLimited,
                "Слишком много запросов к GitHub API.",
                "Опрос возобновится после паузы.",
                IsFatalForRepo: false),

            >= HttpStatusCode.InternalServerError => new(
                GitHubPollErrorCode.ServerError,
                "Временная ошибка сервера GitHub.",
                "Повторим при следующем опросе.",
                IsFatalForRepo: false),

            _ => new(
                GitHubPollErrorCode.Unknown,
                api.Message,
                null,
                IsFatalForRepo: false),
        };
    }

    private static bool IsRateLimitBody(string? body) =>
        body is not null
        && body.Contains("rate limit", StringComparison.OrdinalIgnoreCase);

    private static string ExtractRepoPath(string path)
    {
        const string prefix = "/repos/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var remainder = path[prefix.Length..];
        var slash = remainder.IndexOf('/');
        return slash < 0 ? remainder : remainder[..slash];
    }

    internal static string? TryReadGitHubMessage(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}

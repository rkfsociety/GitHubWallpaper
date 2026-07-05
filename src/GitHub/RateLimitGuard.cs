using System.Net;

namespace GitHubWallpaper.GitHub;

/// <summary>
/// Отслеживает заголовки <c>X-RateLimit-*</c> и откладывает запросы при исчерпании лимита или 403.
/// </summary>
internal sealed class RateLimitGuard
{
    private static readonly TimeSpan ResetBuffer = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FallbackBackoff = TimeSpan.FromMinutes(1);

    private readonly object _sync = new();
    private GitHubRateLimit _current;
    private DateTimeOffset? _backoffUntil;

    /// <summary>Последний известный снимок лимита из ответа GitHub API.</summary>
    public GitHubRateLimit Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    /// <summary>Время, до которого новые запросы откладываются; <c>null</c>, если backoff не активен.</summary>
    public DateTimeOffset? BackoffUntil
    {
        get
        {
            lock (_sync)
            {
                return _backoffUntil;
            }
        }
    }

    /// <summary>Обновляет состояние по заголовкам ответа и при необходимости включает backoff.</summary>
    public void Observe(GitHubRateLimit rateLimit)
    {
        lock (_sync)
        {
            if (HasUsableData(rateLimit))
            {
                _current = rateLimit;
            }

            if (ShouldBackoffFromHeaders(rateLimit))
            {
                SetBackoffUntil(rateLimit.ResetAt!.Value.Add(ResetBuffer));
            }
        }
    }

    /// <summary>
    /// Обрабатывает ошибку ответа: при rate limit 403 или <c>Retry-After</c> включает backoff.
    /// </summary>
    public void HandleErrorResponse(
        HttpStatusCode statusCode,
        GitHubRateLimit rateLimit,
        string? responseBody,
        TimeSpan? retryAfter = null)
    {
        lock (_sync)
        {
            if (HasUsableData(rateLimit))
            {
                _current = rateLimit;
            }

            if (!ShouldBackoffFromError(statusCode, rateLimit, responseBody, retryAfter))
            {
                return;
            }

            var backoffUntil = ResolveBackoffUntil(rateLimit, retryAfter);
            SetBackoffUntil(backoffUntil);
        }
    }

    /// <summary>Ждёт окончания backoff перед следующим запросом к API.</summary>
    public async Task WaitIfNeededAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset? until;
        lock (_sync)
        {
            until = _backoffUntil;
        }

        if (until is null)
        {
            return;
        }

        var delay = until.Value - DateTimeOffset.UtcNow;
        if (delay <= TimeSpan.Zero)
        {
            ClearBackoffIfExpired(until.Value);
            return;
        }

        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        ClearBackoffIfExpired(until.Value);
    }

    private void SetBackoffUntil(DateTimeOffset until)
    {
        if (_backoffUntil is null || until > _backoffUntil)
        {
            _backoffUntil = until;
        }
    }

    private void ClearBackoffIfExpired(DateTimeOffset expectedUntil)
    {
        lock (_sync)
        {
            if (_backoffUntil == expectedUntil && _backoffUntil <= DateTimeOffset.UtcNow)
            {
                _backoffUntil = null;
            }
        }
    }

    private static bool HasUsableData(GitHubRateLimit rateLimit) =>
        rateLimit.Limit is not null
        || rateLimit.Remaining is not null
        || rateLimit.Used is not null
        || rateLimit.ResetAt is not null;

    private static bool ShouldBackoffFromHeaders(GitHubRateLimit rateLimit) =>
        rateLimit.Remaining == 0
        && rateLimit.ResetAt is { } resetAt
        && resetAt > DateTimeOffset.UtcNow;

    private static bool ShouldBackoffFromError(
        HttpStatusCode statusCode,
        GitHubRateLimit rateLimit,
        string? responseBody,
        TimeSpan? retryAfter)
    {
        if (retryAfter is not null)
        {
            return true;
        }

        return statusCode == HttpStatusCode.Forbidden && IsRateLimitResponse(rateLimit, responseBody);
    }

    private static bool IsRateLimitResponse(GitHubRateLimit rateLimit, string? responseBody)
    {
        if (rateLimit.Remaining == 0)
        {
            return true;
        }

        return responseBody is not null
            && responseBody.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset ResolveBackoffUntil(GitHubRateLimit rateLimit, TimeSpan? retryAfter)
    {
        if (retryAfter is { } delay)
        {
            return DateTimeOffset.UtcNow.Add(delay).Add(ResetBuffer);
        }

        if (rateLimit.ResetAt is { } resetAt)
        {
            return resetAt.Add(ResetBuffer);
        }

        return DateTimeOffset.UtcNow.Add(FallbackBackoff);
    }
}

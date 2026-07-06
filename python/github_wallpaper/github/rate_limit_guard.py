"""Отслеживание X-RateLimit-* и backoff при 403 (RateLimitGuard.cs)."""

from __future__ import annotations

import asyncio
from datetime import datetime, timedelta, timezone

from github_wallpaper.github.github_models import GitHubRateLimit

_RESET_BUFFER = timedelta(seconds=1)
_FALLBACK_BACKOFF = timedelta(minutes=1)


class RateLimitGuard:
    """Откладывает запросы при исчерпании лимита GitHub API."""

    def __init__(self) -> None:
        self._lock = asyncio.Lock()
        self._current = GitHubRateLimit()
        self._backoff_until: datetime | None = None

    @property
    def current(self) -> GitHubRateLimit:
        return self._current

    @property
    def backoff_until(self) -> datetime | None:
        return self._backoff_until

    def observe(self, rate_limit: GitHubRateLimit) -> None:
        if rate_limit.has_usable_data:
            self._current = rate_limit

        if _should_backoff_from_headers(rate_limit):
            self._set_backoff_until(rate_limit.reset_at + _RESET_BUFFER)  # type: ignore[operator]

    def handle_error_response(
        self,
        status_code: int,
        rate_limit: GitHubRateLimit,
        response_body: str | None,
        retry_after: timedelta | None = None,
    ) -> None:
        if rate_limit.has_usable_data:
            self._current = rate_limit

        if not _should_backoff_from_error(status_code, rate_limit, response_body, retry_after):
            return

        self._set_backoff_until(_resolve_backoff_until(rate_limit, retry_after))

    async def wait_if_needed(self) -> None:
        until = self._backoff_until
        if until is None:
            return

        now = datetime.now(timezone.utc)
        delay = until - now
        if delay <= timedelta(0):
            self._clear_backoff_if_expired(until)
            return

        await asyncio.sleep(delay.total_seconds())
        self._clear_backoff_if_expired(until)

    def _set_backoff_until(self, until: datetime) -> None:
        if self._backoff_until is None or until > self._backoff_until:
            self._backoff_until = until

    def _clear_backoff_if_expired(self, expected_until: datetime) -> None:
        if self._backoff_until == expected_until and self._backoff_until <= datetime.now(timezone.utc):
            self._backoff_until = None


def _should_backoff_from_headers(rate_limit: GitHubRateLimit) -> bool:
    return (
        rate_limit.remaining == 0
        and rate_limit.reset_at is not None
        and rate_limit.reset_at > datetime.now(timezone.utc)
    )


def _should_backoff_from_error(
    status_code: int,
    rate_limit: GitHubRateLimit,
    response_body: str | None,
    retry_after: timedelta | None,
) -> bool:
    if retry_after is not None:
        return True

    return status_code == 403 and _is_rate_limit_response(rate_limit, response_body)


def _is_rate_limit_response(rate_limit: GitHubRateLimit, response_body: str | None) -> bool:
    if rate_limit.remaining == 0:
        return True
    return response_body is not None and "rate limit" in response_body.lower()


def _resolve_backoff_until(rate_limit: GitHubRateLimit, retry_after: timedelta | None) -> datetime:
    now = datetime.now(timezone.utc)
    if retry_after is not None:
        return now + retry_after + _RESET_BUFFER
    if rate_limit.reset_at is not None:
        return rate_limit.reset_at + _RESET_BUFFER
    return now + _FALLBACK_BACKOFF

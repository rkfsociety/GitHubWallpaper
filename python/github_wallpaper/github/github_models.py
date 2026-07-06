"""Модели ответов GitHub REST API (GitHubApiModels.cs)."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime
from typing import Any

import httpx


@dataclass(frozen=True, slots=True)
class GitHubRateLimit:
    limit: int | None = None
    remaining: int | None = None
    used: int | None = None
    reset_at: datetime | None = None

    @property
    def has_usable_data(self) -> bool:
        return any(value is not None for value in (self.limit, self.remaining, self.used, self.reset_at))

    @classmethod
    def try_parse(cls, response: httpx.Response) -> GitHubRateLimit:
        return cls(
            limit=_parse_int_header(response, "x-ratelimit-limit"),
            remaining=_parse_int_header(response, "x-ratelimit-remaining"),
            used=_parse_int_header(response, "x-ratelimit-used"),
            reset_at=_parse_reset_at(response),
        )


@dataclass(frozen=True, slots=True)
class GitHubApiResult:
    status_code: int
    body: str
    rate_limit: GitHubRateLimit
    etag: str | None
    is_not_modified: bool = False


class GitHubApiException(Exception):
    def __init__(
        self,
        status_code: int,
        path: str,
        response_body: str | None,
        rate_limit: GitHubRateLimit,
    ) -> None:
        self.status_code = status_code
        self.path = path
        self.response_body = response_body
        self.rate_limit = rate_limit
        super().__init__(_build_message(status_code, path, response_body))


def _build_message(status_code: int, path: str, response_body: str | None) -> str:
    if not response_body:
        return f"GitHub API вернул {status_code} для {path}."
    return f"GitHub API вернул {status_code} для {path}: {response_body}"


def _parse_int_header(response: httpx.Response, name: str) -> int | None:
    value = response.headers.get(name)
    if value is None:
        return None
    try:
        return int(value)
    except ValueError:
        return None


def _parse_reset_at(response: httpx.Response) -> datetime | None:
    value = response.headers.get("x-ratelimit-reset")
    if value is None:
        return None
    try:
        return datetime.fromtimestamp(int(value), tz=timezone.utc)
    except ValueError:
        return None


def try_parse_retry_after(response: httpx.Response) -> Any:
    from datetime import timedelta

    header = response.headers.get("retry-after")
    if header is None:
        return None

    try:
        return timedelta(seconds=int(header))
    except ValueError:
        pass

    try:
        date = parsedate_to_datetime(header)
        if date.tzinfo is None:
            date = date.replace(tzinfo=timezone.utc)
        delay = date - datetime.now(timezone.utc)
        return delay if delay.total_seconds() > 0 else timedelta(0)
    except (TypeError, ValueError, OverflowError):
        return None

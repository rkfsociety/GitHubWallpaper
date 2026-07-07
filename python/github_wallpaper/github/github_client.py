"""HTTP-клиент GitHub REST API с ETag и Authorization (GitHubApiClient.cs)."""

from __future__ import annotations

import asyncio
from importlib.metadata import version

import httpx

from github_wallpaper.github.github_models import (
    GitHubApiException,
    GitHubApiResult,
    GitHubRateLimit,
    try_parse_retry_after,
)
from github_wallpaper.github.rate_limit_guard import RateLimitGuard

API_BASE_URL = "https://api.github.com"
_GITHUB_API_VERSION = "2022-11-28"


def _build_user_agent() -> str:
    try:
        app_version = version("github-wallpaper")
    except Exception:
        app_version = "2.0.0"
    return f"GitHubWallpaper/{app_version}"


class GitHubClient:
    """httpx-клиент для GitHub REST API."""

    def __init__(
        self,
        token: str | None = None,
        *,
        client: httpx.AsyncClient | None = None,
        rate_limit_guard: RateLimitGuard | None = None,
    ) -> None:
        self._token = _normalize_token(token)
        self._rate_limit_guard = rate_limit_guard or RateLimitGuard()
        self._owns_client = client is None
        self._client = client or httpx.AsyncClient(
            base_url=API_BASE_URL,
            headers={
                "Accept": "application/vnd.github+json",
                "X-GitHub-Api-Version": _GITHUB_API_VERSION,
                "User-Agent": _build_user_agent(),
            },
            timeout=httpx.Timeout(30.0),
        )

    @property
    def has_token(self) -> bool:
        return self._token is not None

    @property
    def token(self) -> str | None:
        return self._token

    @property
    def rate_limit(self) -> RateLimitGuard:
        return self._rate_limit_guard

    def set_token(self, token: str | None) -> None:
        self._token = _normalize_token(token)

    async def get(
        self,
        path: str,
        *,
        if_none_match: str | None = None,
    ) -> GitHubApiResult:
        await self._rate_limit_guard.wait_if_needed()

        headers: dict[str, str] = {}
        if self._token is not None:
            headers["Authorization"] = f"Bearer {self._token}"
        if if_none_match:
            headers["If-None-Match"] = if_none_match

        normalized_path = path if path.startswith("/") else f"/{path}"
        response = await self._client.get(normalized_path, headers=headers)
        body = response.text
        rate_limit = GitHubRateLimit.try_parse(response)
        etag = response.headers.get("etag") or if_none_match

        if response.status_code == 304:
            self._rate_limit_guard.observe(rate_limit)
            return GitHubApiResult(
                status_code=response.status_code,
                body="",
                rate_limit=rate_limit,
                etag=etag,
                is_not_modified=True,
            )

        if response.status_code >= 400:
            self._rate_limit_guard.handle_error_response(
                response.status_code,
                rate_limit,
                body,
                try_parse_retry_after(response),
            )
            raise GitHubApiException(response.status_code, normalized_path, body, rate_limit)

        self._rate_limit_guard.observe(rate_limit)
        return GitHubApiResult(
            status_code=response.status_code,
            body=body,
            rate_limit=rate_limit,
            etag=etag,
        )

    async def get_stats(
        self,
        path: str,
        *,
        if_none_match: str | None = None,
    ) -> GitHubApiResult | None:
        max_attempts = 12

        for attempt in range(max_attempts):
            result = await self.get(path, if_none_match=if_none_match)

            if result.is_not_modified or result.status_code != 202:
                return result

            if attempt == max_attempts - 1:
                break

            delay = min(8, 2 + attempt)
            await asyncio.sleep(delay)

        return None

    async def get_authenticated_user(self) -> GitHubApiResult:
        return await self.get("/user")

    async def aclose(self) -> None:
        if self._owns_client:
            await self._client.aclose()

    async def __aenter__(self) -> GitHubClient:
        return self

    async def __aexit__(self, *args: object) -> None:
        await self.aclose()


def _normalize_token(token: str | None) -> str | None:
    if token is None or not token.strip():
        return None
    return token.strip()

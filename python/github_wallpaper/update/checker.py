"""Проверка GitHub Release latest на наличие новой версии."""

from __future__ import annotations

import json
import re
from typing import Any

import httpx

from github_wallpaper.update.asset_download import ascii_user_agent
from github_wallpaper.update import app_version, defaults
from github_wallpaper.update.models import (
    AppUpdateCheckResult,
    AppUpdateInfo,
    Failed,
    Skipped,
    UpToDate,
    UpdateAvailable,
)

_VERSION_IN_NOTES_RE = re.compile(
    r"\*\*(?P<version>\d+\.\d+\.\d+(?:[-\w.]*)?)\*\*",
    re.IGNORECASE,
)
_RELEASE_NAME_VERSION_RE = re.compile(
    r"GitHubWallpaper\s+(?P<version>\d+\.\d+\.\d+(?:[-\w.]*)?)",
    re.IGNORECASE,
)


def fetch_latest_release_version(*, client: httpx.Client | None = None) -> str | None:
    """Версия runtime из релиза latest (version.json или заметки релиза)."""
    owns_client = client is None
    http_client = client or AppUpdateChecker._create_client(None)
    try:
        response = http_client.get(defaults.release_api_url())
        response.raise_for_status()
        release = response.json()
        version_json_url = _find_asset_url(release, defaults.VERSION_ASSET_NAME)
        return _resolve_remote_version(release, version_json_url, http_client)
    except httpx.HTTPError:
        return None
    finally:
        if owns_client:
            http_client.close()


class AppUpdateChecker:
    """Синхронная проверка релиза latest через GitHub REST API."""

    def __init__(self, *, client: httpx.Client | None = None, token: str | None = None) -> None:
        self._owns_client = client is None
        self._token = _normalize_token(token)
        if client is None:
            self._client = self._create_client(self._token)
            return

        self._client = client
        if self._token:
            self._client.headers["Authorization"] = f"Bearer {self._token}"

    def set_token(self, token: str | None) -> None:
        normalized = _normalize_token(token)
        if normalized == self._token:
            return

        self._token = normalized
        if not self._owns_client:
            if self._token:
                self._client.headers["Authorization"] = f"Bearer {self._token}"
            else:
                self._client.headers.pop("Authorization", None)
            return

        self._client.close()
        self._client = self._create_client(self._token)

    def check(self) -> AppUpdateCheckResult:
        if not app_version.can_self_update():
            return Skipped(
                "Автообновление доступно только в собранной portable-версии GitHubWallpaper.",
            )

        if app_version.is_development_build():
            return Skipped(
                f"Сборка для разработки ({app_version.current_version()}) "
                "не обновляется автоматически.",
            )

        try:
            response = self._client.get(defaults.release_api_url())
            if response.status_code == 403 and _is_rate_limit_body(response.text):
                return Failed(
                    "Исчерпан лимит запросов GitHub API. "
                    "Добавьте PAT в настройках или повторите позже.",
                )
            if response.status_code == 401:
                return Failed("Токен GitHub недействителен. Откройте Настройки и обновите PAT.")
            if response.status_code >= 400:
                return Failed(f"GitHub API вернул HTTP {response.status_code}.")

            release = response.json()
            asset_name = defaults.platform_archive_asset_name()
            archive_asset = _find_asset(release, asset_name)
            if archive_asset is None:
                return Failed(f"В релизе не найден {asset_name}.")

            version_json_url = _find_asset_url(release, defaults.VERSION_ASSET_NAME)
            remote_version = _resolve_remote_version(release, version_json_url, self._client)
            if not remote_version:
                return Failed("Не удалось определить версию релиза.")

            current = app_version.current_version()
            if not app_version.is_remote_newer(remote_version):
                return UpToDate(current)

            release_page = release.get("html_url") or defaults.release_page_url()
            asset_id = archive_asset.get("id")
            return UpdateAvailable(
                AppUpdateInfo(
                    version=remote_version,
                    download_url=str(archive_asset["browser_download_url"]),
                    release_page_url=str(release_page),
                    asset_size_bytes=_read_asset_size(archive_asset),
                    asset_name=asset_name,
                    asset_id=int(asset_id) if isinstance(asset_id, int) else None,
                ),
                current,
            )
        except httpx.HTTPError as ex:
            return Failed(f"Не удалось проверить обновления: {ex}")
        except json.JSONDecodeError:
            return Failed("GitHub API вернул некорректный JSON.")

    def close(self) -> None:
        if self._owns_client:
            self._client.close()

    def __enter__(self) -> AppUpdateChecker:
        return self

    def __exit__(self, *args: object) -> None:
        self.close()

    @staticmethod
    def _create_client(token: str | None) -> httpx.Client:
        headers = {
            "Accept": "application/vnd.github+json",
            "User-Agent": ascii_user_agent(),
        }
        if token:
            headers["Authorization"] = f"Bearer {token}"

        return httpx.Client(
            headers=headers,
            timeout=httpx.Timeout(30.0),
            follow_redirects=True,
        )


def _normalize_token(token: str | None) -> str | None:
    if not token:
        return None
    stripped = token.strip()
    return stripped or None


def _find_asset(release: dict[str, Any], name: str) -> dict[str, Any] | None:
    assets = release.get("assets")
    if not isinstance(assets, list):
        return None

    for asset in assets:
        if isinstance(asset, dict) and asset.get("name") == name:
            return asset
    return None


def _find_asset_url(release: dict[str, Any], name: str) -> str | None:
    asset = _find_asset(release, name)
    if asset is None:
        return None
    url = asset.get("browser_download_url")
    return str(url) if url else None


def _read_asset_size(asset: dict[str, Any]) -> int | None:
    size = asset.get("size")
    if isinstance(size, int):
        return size
    return None


def _resolve_remote_version(
    release: dict[str, Any],
    version_json_url: str | None,
    client: httpx.Client,
) -> str | None:
    if version_json_url:
        try:
            response = client.get(version_json_url)
            if response.is_success:
                parsed = _parse_version_json(response.text)
                if parsed:
                    return parsed
        except httpx.HTTPError:
            pass

    body = release.get("body")
    if isinstance(body, str):
        match = _VERSION_IN_NOTES_RE.search(body)
        if match:
            return match.group("version")

    name = release.get("name")
    if isinstance(name, str):
        match = _RELEASE_NAME_VERSION_RE.search(name)
        if match:
            return match.group("version")

    return None


def _parse_version_json(text: str) -> str | None:
    try:
        payload = json.loads(text)
    except json.JSONDecodeError:
        return None

    if isinstance(payload, dict):
        value = payload.get("version")
        if isinstance(value, str) and value.strip():
            return value.strip()
    return None


def _is_rate_limit_body(body: str | None) -> bool:
    return body is not None and "rate limit" in body.lower()

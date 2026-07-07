"""Скачивание release-артефактов GitHub (browser URL или API + PAT)."""

from __future__ import annotations

from collections.abc import Callable
from pathlib import Path

import httpx

from github_wallpaper.github.github_client import _build_user_agent
from github_wallpaper.update import defaults
from github_wallpaper.update.models import AppUpdateDownloadProgress, AppUpdateInfo

DOWNLOAD_TIMEOUT = httpx.Timeout(connect=45.0, read=90.0, write=90.0, pool=30.0)
CHUNK_SIZE = 81_920


def ascii_user_agent() -> str:
    value = _build_user_agent()
    ascii_value = value.encode("ascii", "ignore").decode("ascii").strip()
    return ascii_value or "GitHubWallpaper/2.0"


def build_download_headers(*, token: str | None = None) -> dict[str, str]:
    headers = {"User-Agent": ascii_user_agent()}
    if token and token.strip() and token.strip().isascii():
        headers["Authorization"] = f"Bearer {token.strip()}"
    return headers


def resolve_download_target(
    update: AppUpdateInfo,
    *,
    token: str | None = None,
) -> tuple[str, dict[str, str]]:
    """Предпочитает GitHub API asset endpoint с PAT — стабильнее browser_download_url."""
    if update.asset_id is not None:
        url = (
            f"https://api.github.com/repos/{defaults.OWNER}/{defaults.REPO}"
            f"/releases/assets/{update.asset_id}"
        )
        headers = build_download_headers(token=token)
        headers["Accept"] = "application/octet-stream"
        return url, headers

    return update.download_url, build_download_headers(token=token)


def stream_archive_to_file(
    update: AppUpdateInfo,
    target_path: Path,
    *,
    token: str | None = None,
    progress: Callable[[AppUpdateDownloadProgress], None] | None = None,
) -> None:
    download_url, headers = resolve_download_target(update, token=token)

    with httpx.Client(
        http2=False,
        follow_redirects=True,
        timeout=DOWNLOAD_TIMEOUT,
        headers=headers,
    ) as client:
        with client.stream("GET", download_url) as response:
            response.raise_for_status()
            total_bytes = response.headers.get("Content-Length")
            expected_total = int(total_bytes) if total_bytes else update.asset_size_bytes

            if progress is not None:
                progress(AppUpdateDownloadProgress(0, expected_total))

            downloaded = 0
            with target_path.open("wb") as output:
                for chunk in response.iter_bytes(CHUNK_SIZE):
                    if not chunk:
                        continue
                    output.write(chunk)
                    downloaded += len(chunk)
                    if progress is not None:
                        progress(AppUpdateDownloadProgress(downloaded, expected_total))

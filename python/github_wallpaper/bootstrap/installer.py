"""Скачивание и распаковка runtime-архива из GitHub Release."""

from __future__ import annotations

import shutil
import tarfile
import zipfile
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import httpx

from github_wallpaper.bootstrap.runtime_paths import runtime_root
from github_wallpaper.bootstrap.runtime_version import write_installed_version
from github_wallpaper.github.github_client import _build_user_agent
from github_wallpaper.update import defaults
from github_wallpaper.update.checker import _find_asset_url, _resolve_remote_version
from github_wallpaper.update.installer import (
    CHUNK_SIZE,
    MAX_DOWNLOAD_ATTEMPTS,
    _is_retryable_download_error,
    update_work_directory,
)
from github_wallpaper.update.models import AppUpdateDownloadProgress

ProgressCallback = Callable[[AppUpdateDownloadProgress], None]


@dataclass(frozen=True, slots=True)
class RuntimeReleaseInfo:
    version: str | None
    download_url: str
    asset_name: str
    asset_size_bytes: int | None


def fetch_runtime_release() -> RuntimeReleaseInfo:
    headers = {
        "Accept": "application/vnd.github+json",
        "User-Agent": _build_user_agent(),
    }
    with httpx.Client(headers=headers, timeout=httpx.Timeout(30.0), follow_redirects=True) as client:
        response = client.get(defaults.release_api_url())
        response.raise_for_status()
        release = response.json()
        asset_name = defaults.platform_archive_asset_name()
        asset = _find_asset(release, asset_name)
        if asset is None:
            raise FileNotFoundError(f"В релизе не найден {asset_name}.")

        version_json_url = _find_asset_url(release, defaults.VERSION_ASSET_NAME)
        version = _resolve_remote_version(release, version_json_url, client)
        size = asset.get("size")
        return RuntimeReleaseInfo(
            version=version,
            download_url=str(asset["browser_download_url"]),
            asset_name=asset_name,
            asset_size_bytes=int(size) if isinstance(size, int) else None,
        )


def fetch_runtime_asset() -> tuple[str, str, int | None]:
    release = fetch_runtime_release()
    return release.download_url, release.asset_name, release.asset_size_bytes


def runtime_needs_update() -> bool:
    """True, если на GitHub есть runtime новее установленного в AppData."""
    from github_wallpaper.bootstrap.runtime_version import needs_update, read_installed_version

    release = fetch_runtime_release()
    if not release.version:
        return False
    return needs_update(release.version, read_installed_version())


def install_runtime(progress: ProgressCallback | None = None) -> Path:
    release = fetch_runtime_release()
    archive_path = _download_archive(
        release.download_url,
        release.asset_name,
        release.asset_size_bytes,
        progress,
    )
    target_dir = _extract_runtime(archive_path)
    if release.version:
        write_installed_version(release.version)
    return target_dir


def _download_archive(
    download_url: str,
    asset_name: str,
    asset_size: int | None,
    progress: ProgressCallback | None,
) -> Path:
    last_error: Exception | None = None

    for attempt in range(1, MAX_DOWNLOAD_ATTEMPTS + 1):
        try:
            return _download_archive_once(download_url, asset_name, asset_size, progress)
        except Exception as ex:
            if attempt >= MAX_DOWNLOAD_ATTEMPTS or not _is_retryable_download_error(ex):
                raise
            last_error = ex
            if progress is not None:
                progress(AppUpdateDownloadProgress(0, asset_size))

    raise last_error or RuntimeError("Не удалось скачать runtime.")


def _download_archive_once(
    download_url: str,
    asset_name: str,
    asset_size: int | None,
    progress: ProgressCallback | None,
) -> Path:
    work_dir = update_work_directory()
    target_path = work_dir / asset_name
    if target_path.exists():
        target_path.unlink()

    headers = {"User-Agent": _build_user_agent()}
    with httpx.stream(
        "GET",
        download_url,
        headers=headers,
        timeout=httpx.Timeout(15 * 60.0),
        follow_redirects=True,
    ) as response:
        response.raise_for_status()
        total_bytes = response.headers.get("Content-Length")
        expected_total = int(total_bytes) if total_bytes else asset_size

        downloaded = 0
        with target_path.open("wb") as output:
            for chunk in response.iter_bytes(CHUNK_SIZE):
                output.write(chunk)
                downloaded += len(chunk)
                if progress is not None:
                    progress(AppUpdateDownloadProgress(downloaded, expected_total))

    file_size = target_path.stat().st_size
    if file_size < defaults.MINIMUM_ARCHIVE_SIZE_BYTES:
        target_path.unlink(missing_ok=True)
        raise ValueError("Скачанный файл слишком маленький.")

    if expected_total is not None and file_size != expected_total:
        target_path.unlink(missing_ok=True)
        raise ValueError("Файл runtime загружен не полностью.")

    if asset_size and file_size != asset_size:
        target_path.unlink(missing_ok=True)
        raise ValueError("Размер скачанного runtime не совпадает с релизом.")

    return target_path


def _extract_runtime(archive_path: Path) -> Path:
    extract_dir = update_work_directory() / "bootstrap-extracted"
    if extract_dir.exists():
        shutil.rmtree(extract_dir, ignore_errors=True)
    extract_dir.mkdir(parents=True, exist_ok=True)

    if archive_path.suffix.lower() == ".zip":
        with zipfile.ZipFile(archive_path) as archive:
            archive.extractall(extract_dir)
    else:
        with tarfile.open(archive_path, "r:*") as archive:
            archive.extractall(extract_dir, filter="data")

    payload_dir = _find_payload_directory(extract_dir)
    target_dir = runtime_root()
    if target_dir.exists():
        shutil.rmtree(target_dir, ignore_errors=True)
    target_dir.parent.mkdir(parents=True, exist_ok=True)
    shutil.copytree(payload_dir, target_dir)
    return target_dir


def _find_payload_directory(root: Path) -> Path:
    if _contains_executable(root):
        return root

    children = [path for path in root.iterdir() if path.is_dir()]
    for child in children:
        if _contains_executable(child):
            return child

    if len(children) == 1:
        return children[0]
    return root


def _contains_executable(directory: Path) -> bool:
    names = ("GitHubWallpaper.exe", "GitHubWallpaper") if __import__("sys").platform == "win32" else ("GitHubWallpaper",)
    return any((directory / name).is_file() for name in names)


def _find_asset(release: dict[str, Any], name: str) -> dict[str, Any] | None:
    assets = release.get("assets")
    if not isinstance(assets, list):
        return None

    for asset in assets:
        if isinstance(asset, dict) and asset.get("name") == name:
            return asset
    return None

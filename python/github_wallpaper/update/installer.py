"""Скачивание архива обновления и перезапуск приложения."""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tarfile
import tempfile
import time
import zipfile
from collections.abc import Callable
from pathlib import Path

import httpx

from github_wallpaper.github.github_client import _build_user_agent
from github_wallpaper.update import app_version, defaults
from github_wallpaper.update.models import AppUpdateDownloadProgress, AppUpdateInfo

MAX_DOWNLOAD_ATTEMPTS = 3
CHUNK_SIZE = 81_920


def update_work_directory() -> Path:
    base = Path(tempfile.gettempdir()) / "GitHubWallpaper" / "update"
    base.mkdir(parents=True, exist_ok=True)
    return base


def download(
    update: AppUpdateInfo,
    progress: Callable[[AppUpdateDownloadProgress], None] | None = None,
) -> Path:
    last_error: Exception | None = None

    for attempt in range(1, MAX_DOWNLOAD_ATTEMPTS + 1):
        try:
            return _download_once(update, progress)
        except Exception as ex:
            if attempt >= MAX_DOWNLOAD_ATTEMPTS or not _is_retryable_download_error(ex):
                raise
            last_error = ex
            if progress is not None:
                progress(AppUpdateDownloadProgress(0, update.asset_size_bytes))
            time.sleep(attempt * 2)

    raise last_error or RuntimeError("Не удалось скачать обновление.")


def schedule_restart(archive_path: Path) -> None:
    install_dir = app_version.install_directory()
    extracted_root = _extract_archive(archive_path)
    payload_dir = _find_payload_directory(extracted_root)
    executable = _find_executable(payload_dir)
    if executable is None:
        raise FileNotFoundError("В архиве обновления не найден исполняемый файл GitHubWallpaper.")

    if sys.platform == "win32":
        _schedule_restart_windows(install_dir, payload_dir, executable)
        return

    _schedule_restart_unix(install_dir, payload_dir, executable)


def format_download_error(exc: Exception) -> str:
    message = str(exc)
    if "response ended" in message.lower():
        return "Соединение оборвалось во время скачивания. Проверьте интернет и повторите."
    if isinstance(exc, httpx.TimeoutException):
        return "Истекло время ожидания ответа GitHub. Повторите позже."
    return message


def _download_once(
    update: AppUpdateInfo,
    progress: Callable[[AppUpdateDownloadProgress], None] | None,
) -> Path:
    work_dir = update_work_directory()
    target_path = work_dir / update.asset_name
    if target_path.exists():
        target_path.unlink()

    headers = {"User-Agent": _build_user_agent()}
    with httpx.stream(
        "GET",
        update.download_url,
        headers=headers,
        timeout=httpx.Timeout(15 * 60.0),
        follow_redirects=True,
    ) as response:
        response.raise_for_status()
        total_bytes = response.headers.get("Content-Length")
        expected_total = int(total_bytes) if total_bytes else update.asset_size_bytes

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
        raise ValueError("Скачанный файл слишком маленький — обновление отменено.")

    if expected_total is not None and file_size != expected_total:
        target_path.unlink(missing_ok=True)
        raise ValueError(
            f"Скачано {file_size} из {expected_total} байт — файл загружен не полностью.",
        )

    if update.asset_size_bytes and file_size != update.asset_size_bytes:
        target_path.unlink(missing_ok=True)
        raise ValueError(
            f"Скачано {file_size} из {update.asset_size_bytes} байт — файл загружен не полностью.",
        )

    return target_path


def _extract_archive(archive_path: Path) -> Path:
    extract_dir = update_work_directory() / "extracted"
    if extract_dir.exists():
        shutil.rmtree(extract_dir, ignore_errors=True)
    extract_dir.mkdir(parents=True, exist_ok=True)

    if archive_path.suffix.lower() == ".zip":
        with zipfile.ZipFile(archive_path) as archive:
            archive.extractall(extract_dir)
        return extract_dir

    with tarfile.open(archive_path, "r:*") as archive:
        archive.extractall(extract_dir, filter="data")
    return extract_dir


def _find_payload_directory(root: Path) -> Path:
    if _find_executable(root) is not None:
        return root

    children = [path for path in root.iterdir() if path.is_dir()]
    for child in children:
        if _find_executable(child) is not None:
            return child

    if len(children) == 1:
        return children[0]
    return root


def _find_executable(directory: Path) -> Path | None:
    names = ("GitHubWallpaper.exe", "GitHubWallpaper") if sys.platform == "win32" else ("GitHubWallpaper",)
    for name in names:
        candidate = directory / name
        if candidate.is_file():
            return candidate
    return None


def _schedule_restart_windows(install_dir: Path, payload_dir: Path, executable: Path) -> None:
    script_path = update_work_directory() / "apply-update.cmd"
    script = f"""@echo off
setlocal
ping 127.0.0.1 -n 3 >nul
robocopy "{payload_dir}" "{install_dir}" /MIR /NFL /NDL /NJH /NJS /nc /ns /np >nul
if errorlevel 8 exit /b 1
start "" "{install_dir / executable.name}"
del "%~f0"
"""
    script_path.write_text(script, encoding="utf-8")

    subprocess.Popen(
        ["cmd.exe", "/c", str(script_path)],
        creationflags=subprocess.CREATE_NO_WINDOW,
        close_fds=True,
    )


def _schedule_restart_unix(install_dir: Path, payload_dir: Path, executable: Path) -> None:
    script_path = update_work_directory() / "apply-update.sh"
    target_executable = install_dir / executable.name
    script = f"""#!/bin/sh
sleep 2
rm -rf "{install_dir}"/*
cp -a "{payload_dir}"/. "{install_dir}"/
chmod +x "{target_executable}"
nohup "{target_executable}" >/dev/null 2>&1 &
rm -f "$0"
"""
    script_path.write_text(script, encoding="utf-8")
    os.chmod(script_path, 0o755)

    subprocess.Popen(
        ["/bin/sh", str(script_path)],
        start_new_session=True,
        close_fds=True,
    )


def _is_retryable_download_error(exc: Exception) -> bool:
    if isinstance(exc, (httpx.HTTPError, OSError, ValueError)):
        return True
    return "response ended" in str(exc).lower()

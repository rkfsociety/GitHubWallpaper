"""Каталоги bootstrap-установки: launcher в AppData и runtime рядом."""

from __future__ import annotations

import sys
from pathlib import Path

from github_wallpaper.paths import config_dir

_APP_SUBDIR = "app"


def launcher_path() -> Path:
    name = "GitHubWallpaper.exe" if sys.platform == "win32" else "GitHubWallpaper"
    return config_dir() / name


def runtime_root() -> Path:
    return config_dir() / _APP_SUBDIR


def runtime_executable() -> Path | None:
    names = ("GitHubWallpaper.exe", "GitHubWallpaper") if sys.platform == "win32" else ("GitHubWallpaper",)
    candidates = [runtime_root() / name for name in names]
    candidates.extend(runtime_root() / "GitHubWallpaper" / name for name in names)
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    return None


def is_runtime_installed() -> bool:
    return runtime_executable() is not None


def is_running_from_runtime() -> bool:
    if not getattr(sys, "frozen", False):
        return False

    current = Path(sys.executable).resolve()
    installed = runtime_executable()
    if installed is None:
        return False
    return current == installed.resolve()


def preferred_launcher_path() -> Path:
    launcher = launcher_path()
    if launcher.is_file():
        return launcher
    return Path(sys.executable).resolve()

"""Текущая версия запущенного приложения v2.0."""

from __future__ import annotations

import re
import sys
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path

_VERSION_RE = re.compile(r"(\d+)\.(\d+)\.(\d+)")


def current_version() -> str:
    if getattr(sys, "frozen", False):
        try:
            from github_wallpaper import _build_version

            return _build_version.__version__
        except ImportError:
            env_version = _read_version_from_env()
            if env_version:
                return env_version

    try:
        return version("github-wallpaper")
    except PackageNotFoundError:
        from github_wallpaper import __version__

        return __version__


def can_self_update() -> bool:
    if not getattr(sys, "frozen", False):
        return False

    executable = Path(sys.executable).resolve()
    return executable.is_file() and executable.name.lower().startswith("githubwallpaper")


def is_development_build() -> bool:
    value = current_version().lower()
    if any(marker in value for marker in ("-dev", "-pr.")):
        return True
    return bool(
        re.search(r"\d+a\d+", value)
        or re.search(r"\d+b\d+", value)
        or re.search(r"\d+rc\d+", value),
    )


def try_parse_version(text: str | None) -> tuple[int, int, int] | None:
    if not text or not text.strip():
        return None

    cleaned = text.strip().split("+", 1)[0]
    match = _VERSION_RE.search(cleaned)
    if not match:
        return None

    return int(match.group(1)), int(match.group(2)), int(match.group(3))


def is_remote_newer(remote_version: str) -> bool:
    current = try_parse_version(current_version())
    remote = try_parse_version(remote_version)
    if current is None or remote is None:
        return False
    return remote > current


def install_directory() -> Path:
    return Path(sys.executable).resolve().parent


def _read_version_from_env() -> str | None:
    for key in ("GITHUB_WALLPAPER_VERSION", "APP_VERSION"):
        value = __import__("os").environ.get(key)
        if value and value.strip():
            return value.strip()
    return None

"""Версия установленного runtime и сравнение с GitHub Release."""

from __future__ import annotations

import json
from pathlib import Path

from github_wallpaper.bootstrap.runtime_paths import runtime_root
from github_wallpaper.paths import config_dir
from github_wallpaper.update import app_version

_MARKER_NAME = "runtime-version.json"


def marker_path() -> Path:
    return config_dir() / _MARKER_NAME


def read_installed_version() -> str | None:
    """Версия runtime в AppData: marker-файл или METADATA dist-info."""
    path = marker_path()
    if path.is_file():
        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            payload = None
        if isinstance(payload, dict):
            value = payload.get("version")
            if isinstance(value, str) and value.strip():
                return value.strip()

    return _read_dist_info_version(runtime_root())


def write_installed_version(version: str) -> None:
    config_dir().mkdir(parents=True, exist_ok=True)
    marker_path().write_text(
        json.dumps({"version": version.strip()}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def needs_update(remote_version: str, installed_version: str | None = None) -> bool:
    if installed_version is None:
        installed_version = read_installed_version()
    if not installed_version:
        return True
    return app_version.is_version_newer_than(remote_version, installed_version)


def _read_dist_info_version(runtime_dir: Path) -> str | None:
    internal = runtime_dir / "_internal"
    if not internal.is_dir():
        return None

    for metadata in sorted(internal.glob("github_wallpaper-*.dist-info/METADATA")):
        try:
            for line in metadata.read_text(encoding="utf-8").splitlines():
                if line.startswith("Version:"):
                    value = line.split(":", 1)[1].strip()
                    return value or None
        except OSError:
            continue
    return None

"""Каталоги приложения: Windows %APPDATA% и Linux XDG."""

from __future__ import annotations

import os
import shutil
import sys
from pathlib import Path

APP_NAME = "GitHubWallpaper"


def _xdg_path(env_var: str, default: Path) -> Path:
    value = os.environ.get(env_var)
    if value:
        return Path(value).expanduser()
    return default.expanduser()


def config_dir() -> Path:
    """Каталог настроек (settings.json)."""
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA")
        if not appdata:
            raise RuntimeError("Переменная окружения APPDATA не задана.")
        return Path(appdata) / APP_NAME

    return _xdg_path("XDG_CONFIG_HOME", Path.home() / ".config") / APP_NAME


def data_dir() -> Path:
    """Каталог данных (wwwroot, ресурсы)."""
    if sys.platform == "win32":
        return config_dir()

    return _xdg_path("XDG_DATA_HOME", Path.home() / ".local" / "share") / APP_NAME


def cache_dir() -> Path:
    """Каталог кэша (single-instance socket на Linux)."""
    if sys.platform == "win32":
        return config_dir() / "cache"

    return _xdg_path("XDG_CACHE_HOME", Path.home() / ".cache") / APP_NAME


def settings_file() -> Path:
    return config_dir() / "settings.json"


def installed_wallpaper_root() -> Path:
    return data_dir() / "wwwroot" / "wallpaper"


def repo_root() -> Path | None:
    """Корень репозитория при запуске из исходников."""
    candidate = Path(__file__).resolve().parent.parent.parent
    if (candidate / "wwwroot" / "wallpaper" / "index.html").is_file():
        return candidate
    return None


def is_development_build() -> bool:
    if getattr(sys, "frozen", False):
        return False
    return repo_root() is not None


def wallpaper_root() -> Path:
    if is_development_build():
        root = repo_root()
        assert root is not None
        return root / "wwwroot" / "wallpaper"
    return installed_wallpaper_root()


def find_bundled_wallpaper_root() -> Path | None:
    candidates: list[Path] = []

    if getattr(sys, "frozen", False):
        meipass = getattr(sys, "_MEIPASS", None)
        if meipass:
            candidates.append(Path(meipass) / "wwwroot" / "wallpaper")

    candidates.append(Path(sys.executable).resolve().parent / "wwwroot" / "wallpaper")

    for candidate in candidates:
        if (candidate / "index.html").is_file():
            return candidate

    return None


def ensure_wallpaper_assets() -> None:
    """Копирует wwwroot/wallpaper в каталог данных (аналог AppInstaller.EnsureWallpaperAssets)."""
    if is_development_build():
        return

    source = find_bundled_wallpaper_root()
    if source is None:
        return

    target = installed_wallpaper_root()
    target.mkdir(parents=True, exist_ok=True)

    for src_file in source.iterdir():
        if src_file.is_file():
            shutil.copy2(src_file, target / src_file.name)

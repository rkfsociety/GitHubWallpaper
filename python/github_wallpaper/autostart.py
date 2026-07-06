"""Автозапуск: registry Run (Windows) · .desktop в autostart (Linux)."""

from __future__ import annotations

import os
import sys
from pathlib import Path


def is_enabled() -> bool:
    if sys.platform == "win32":
        return _windows_is_enabled()
    return _linux_is_enabled()


def set_enabled(enabled: bool) -> None:
    if sys.platform == "win32":
        _windows_set_enabled(enabled)
    else:
        _linux_set_enabled(enabled)


def refresh_path_if_enabled() -> None:
    if is_enabled():
        set_enabled(True)


def _executable_path() -> str:
    if getattr(sys, "frozen", False):
        return str(Path(sys.executable).resolve())

    module_path = Path(sys.argv[0]).resolve()
    if module_path.exists():
        return str(module_path)

    return str(module_path)


def _quote_path(path: str) -> str:
    return f'"{path}"' if " " in path else path


_RUN_KEY_PATH = r"Software\Microsoft\Windows\CurrentVersion\Run"
_VALUE_NAME = "GitHubWallpaper"


def _windows_is_enabled() -> bool:
    import winreg

    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, _RUN_KEY_PATH, 0, winreg.KEY_READ) as key:
            value, _ = winreg.QueryValueEx(key, _VALUE_NAME)
            return bool(value and str(value).strip())
    except OSError:
        return False


def _windows_set_enabled(enabled: bool) -> None:
    import winreg

    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, _RUN_KEY_PATH, 0, winreg.KEY_SET_VALUE) as key:
        if enabled:
            winreg.SetValueEx(key, _VALUE_NAME, 0, winreg.REG_SZ, _quote_path(_executable_path()))
        else:
            try:
                winreg.DeleteValue(key, _VALUE_NAME)
            except FileNotFoundError:
                pass


def _linux_desktop_path() -> Path:
    config_home = os.environ.get("XDG_CONFIG_HOME")
    base = Path(config_home).expanduser() if config_home else Path.home() / ".config"
    return base / "autostart" / "github-wallpaper.desktop"


def _linux_is_enabled() -> bool:
    return _linux_desktop_path().is_file()


def _linux_set_enabled(enabled: bool) -> None:
    desktop_path = _linux_desktop_path()
    if not enabled:
        if desktop_path.is_file():
            desktop_path.unlink()
        return

    desktop_path.parent.mkdir(parents=True, exist_ok=True)
    exec_path = _quote_path(_executable_path())
    content = (
        "[Desktop Entry]\n"
        "Type=Application\n"
        "Name=GitHub Wallpaper\n"
        "Comment=Dynamic GitHub activity wallpapers\n"
        f"Exec={exec_path}\n"
        "Terminal=false\n"
        "Hidden=false\n"
        "NoDisplay=false\n"
        "X-GNOME-Autostart-enabled=true\n"
    )
    desktop_path.write_text(content, encoding="utf-8")

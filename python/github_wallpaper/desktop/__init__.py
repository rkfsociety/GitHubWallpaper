"""Платформенные бэкенды рабочего стола."""

from github_wallpaper.desktop.backend import DesktopBackend, create_desktop_backend
from github_wallpaper.desktop.screen_helper import ScreenHelper

__all__ = ["DesktopBackend", "ScreenHelper", "create_desktop_backend"]

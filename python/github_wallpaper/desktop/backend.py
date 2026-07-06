"""Абстракция бэкенда рабочего стола: apply / remove / pause / resume / set_screen."""

from __future__ import annotations

import sys
from abc import ABC, abstractmethod

from PySide6.QtGui import QScreen
from PySide6.QtWidgets import QWidget


class DesktopBackend(ABC):
    """Встраивает окно обоев в слой рабочего стола (WorkerW / X11 desktop)."""

    @abstractmethod
    def apply(self, window: QWidget, screen: QScreen) -> None:
        """Прикрепляет окно к рабочему столу на выбранном экране."""

    @abstractmethod
    def remove(self, window: QWidget) -> None:
        """Открепляет окно от рабочего стола."""

    @abstractmethod
    def set_screen(self, window: QWidget, screen: QScreen) -> None:
        """Перемещает обои на другой монитор."""

    def pause(self, window: QWidget) -> None:
        """Платформенная пауза (по умолчанию — no-op; видимость — в контроллере)."""

    def resume(self, window: QWidget) -> None:
        """Платформенное возобновление."""

    def dispose(self) -> None:
        """Освобождает ресурсы бэкенда."""


def create_desktop_backend() -> DesktopBackend:
    """Фабрика бэкенда по текущей ОС."""
    if sys.platform == "win32":
        from github_wallpaper.desktop.windows_backend import WindowsDesktopBackend

        return WindowsDesktopBackend()

    if sys.platform.startswith("linux"):
        from github_wallpaper.desktop.linux_backend import LinuxDesktopBackend

        return LinuxDesktopBackend()

    raise RuntimeError(f"Платформа {sys.platform!r} не поддерживается.")

"""Управление жизненным циклом обоев: apply / pause / resume / remove / set_screen."""

from __future__ import annotations

import logging
from collections.abc import Callable

from PySide6.QtCore import QUrl
from PySide6.QtGui import QGuiApplication, QScreen

from github_wallpaper.desktop.backend import DesktopBackend, create_desktop_backend
from github_wallpaper.desktop.screen_helper import ScreenHelper
from github_wallpaper.paths import wallpaper_root
from github_wallpaper.wallpaper.local_server import LocalWallpaperServer
from github_wallpaper.wallpaper.wallpaper_window import WallpaperWindow

_logger = logging.getLogger(__name__)


class WallpaperController:
    """
    Координирует WallpaperWindow, локальный HTTP-сервер и DesktopBackend.

    Пауза: setVisible(False) на WebEngine + колбэк для снижения polling.
  """

    def __init__(self) -> None:
        self._backend: DesktopBackend = create_desktop_backend()
        self._server: LocalWallpaperServer | None = None
        self._window: WallpaperWindow | None = None
        self._display_device_name: str = ""
        self._paused = False
        self._applied = False
        self._on_polling_pause: Callable[[bool], None] | None = None
        self._screen_hooks_attached = False

    @property
    def is_applied(self) -> bool:
        return self._applied

    @property
    def is_paused(self) -> bool:
        return self._paused

    @property
    def window(self) -> WallpaperWindow | None:
        return self._window

    def set_polling_pause_handler(self, handler: Callable[[bool], None] | None) -> None:
        """Колбэк для RepoPoller: True — снизить polling, False — нормальный режим."""
        self._on_polling_pause = handler

    def configure_display(self, display_device_name: str | None) -> None:
        """Задаёт монитор по DisplayDeviceName из settings.json."""
        self._display_device_name = (display_device_name or "").strip()
        if self._applied and self._window is not None:
            screen = ScreenHelper.resolve(self._display_device_name)
            self._backend.set_screen(self._window, screen)
            self._window.fit_to_screen(screen)

    def apply(self) -> None:
        """Создаёт окно, запускает HTTP-сервер и прикрепляет обои к рабочему столу."""
        screen = ScreenHelper.resolve(self._display_device_name)

        if self._server is None:
            self._server = LocalWallpaperServer(wallpaper_root())
            self._server.start()

        if self._window is None:
            self._window = WallpaperWindow()

        assert self._server is not None
        self._window.load_url(QUrl(self._server.url))
        self._window.fit_to_screen(screen)
        self._window.show()

        self._backend.apply(self._window, screen)
        # WinForms после Show() переприменяет bounds — то же для Qt после SetParent.
        self._backend.set_screen(self._window, screen)
        self._window.mark_attached(True)
        self._ensure_screen_hooks()

        if self._paused:
            self._window.set_render_visible(False)

        self._applied = True
        _logger.info("Обои применены на экране %s", screen.name())

    def pause(self) -> None:
        """Приостанавливает рендер WebEngine и снижает polling."""
        if not self._applied or self._paused or self._window is None:
            return

        self._window.set_render_visible(False)
        self._backend.pause(self._window)
        self._paused = True
        self._notify_polling_pause(True)
        _logger.debug("Обои на паузе")

    def resume(self) -> None:
        """Возобновляет рендер WebEngine и polling."""
        if not self._applied or not self._paused or self._window is None:
            return

        self._window.set_render_visible(True)
        self._backend.resume(self._window)
        self._paused = False
        self._notify_polling_pause(False)
        _logger.debug("Обои возобновлены")

    def remove(self) -> None:
        """Открепляет обои от рабочего стола."""
        if not self._applied or self._window is None:
            return

        self._backend.remove(self._window)
        self._window.mark_attached(False)
        self._window.hide()
        self._applied = False
        _logger.info("Обои откреплены")

    def dispose(self) -> None:
        """Полная очистка ресурсов."""
        if self._window is not None:
            self._backend.remove(self._window)
            self._window.close()
            self._window = None

        if self._server is not None:
            self._server.stop()
            self._server = None

        self._backend.dispose()
        self._remove_screen_hooks()
        self._applied = False
        self._paused = False

    def _notify_polling_pause(self, paused: bool) -> None:
        if self._on_polling_pause is not None:
            self._on_polling_pause(paused)

    def _ensure_screen_hooks(self) -> None:
        if self._screen_hooks_attached:
            return

        app = QGuiApplication.instance()
        if app is not None:
            app.screenAdded.connect(self._on_screens_changed)
            app.screenRemoved.connect(self._on_screens_changed)
            app.primaryScreenChanged.connect(self._on_screens_changed)
        self._screen_hooks_attached = True

    def _remove_screen_hooks(self) -> None:
        if not self._screen_hooks_attached:
            return

        app = QGuiApplication.instance()
        if app is not None:
            app.screenAdded.disconnect(self._on_screens_changed)
            app.screenRemoved.disconnect(self._on_screens_changed)
            app.primaryScreenChanged.disconnect(self._on_screens_changed)
        self._screen_hooks_attached = False

    def _on_screens_changed(self, *_args: object) -> None:
        if not self._applied or self._window is None:
            return

        screen = ScreenHelper.resolve(self._display_device_name)
        self._backend.set_screen(self._window, screen)
        self._window.fit_to_screen(screen)

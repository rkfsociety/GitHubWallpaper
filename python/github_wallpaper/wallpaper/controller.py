"""Управление жизненным циклом обоев: apply / pause / resume / remove / set_screen."""

from __future__ import annotations

import logging
from collections.abc import Callable
from typing import TYPE_CHECKING, Any

from PySide6.QtCore import QUrl, Signal
from PySide6.QtGui import QGuiApplication, QScreen

from github_wallpaper.desktop.backend import DesktopBackend, create_desktop_backend
from github_wallpaper.desktop.screen_helper import ScreenHelper
from github_wallpaper.paths import wallpaper_root
from github_wallpaper.wallpaper.bridge import PendingMessageQueue, deliver_json_to_page
from github_wallpaper.wallpaper.local_server import LocalWallpaperServer
from github_wallpaper.wallpaper.wallpaper_window import WallpaperWindow

if TYPE_CHECKING:
    from github_wallpaper.wallpaper.bridge import WallpaperBridge

_logger = logging.getLogger(__name__)


class WallpaperController:
    """
    Координирует WallpaperWindow, локальный HTTP-сервер и DesktopBackend.

    Пауза: setVisible(False) на WebEngine + колбэк для снижения polling.
    """

    page_ready = Signal()

    def __init__(self) -> None:
        self._backend: DesktopBackend = create_desktop_backend()
        self._server: LocalWallpaperServer | None = None
        self._window: WallpaperWindow | None = None
        self._bridge: WallpaperBridge | None = None
        self._display_device_name: str = ""
        self._paused = False
        self._applied = False
        self._on_polling_pause: Callable[[bool], None] | None = None
        self._screen_hooks_attached = False
        self._message_queue = PendingMessageQueue()

    @property
    def is_applied(self) -> bool:
        return self._applied

    @property
    def is_paused(self) -> bool:
        return self._paused

    @property
    def window(self) -> WallpaperWindow | None:
        return self._window

    @property
    def bridge(self) -> WallpaperBridge | None:
        return self._bridge

    def set_bridge(self, bridge: WallpaperBridge) -> None:
        self._bridge = bridge

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
            self._post_viewport_update(screen)

    def apply(self) -> None:
        """Создаёт окно, запускает HTTP-сервер и прикрепляет обои к рабочему столу."""
        screen = ScreenHelper.resolve(self._display_device_name)

        if self._server is None:
            self._server = LocalWallpaperServer(wallpaper_root())
            self._server.start()

        if self._window is None:
            self._window = WallpaperWindow()
            if self._bridge is not None:
                self._bridge.attach_page(self._window.web_view.page())

        assert self._server is not None
        self._message_queue.reset()
        self._window.load_url(QUrl(self._server.url))
        self._window.fit_to_screen(screen)
        self._window.show()

        self._backend.apply(self._window, screen)
        self._backend.set_screen(self._window, screen)
        self._window.mark_attached(True)
        self._ensure_screen_hooks()

        if self._paused:
            self._window.set_render_visible(False)

        self._applied = True
        self._post_viewport_update(screen)
        _logger.info("Обои применены на экране %s", screen.name())

    def pause(self) -> None:
        """Приостанавливает рендер WebEngine и снижает polling."""
        if not self._applied or self._paused or self._window is None:
            return

        self._window.set_render_visible(False)
        self._backend.pause(self._window)
        self._paused = True
        self._post_wallpaper_command("pause")
        self._notify_polling_pause(True)
        _logger.debug("Обои на паузе")

    def resume(self) -> None:
        """Возобновляет рендер WebEngine и polling."""
        if not self._applied or not self._paused or self._window is None:
            return

        self._window.set_render_visible(True)
        self._backend.resume(self._window)
        self._paused = False
        self._post_wallpaper_command("resume")
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
        self._message_queue.reset()
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
        self._message_queue.reset()

    def notify_page_ready(self) -> None:
        """Вызывается после handshake page:ready из JS."""
        first_ready = not self._message_queue.page_ready
        self._message_queue.mark_ready()

        if first_ready:
            self.page_ready.emit()

        self._flush_pending_messages()

    def post_message_as_json(self, message: Any) -> None:
        """Отправляет JSON в страницу обоев (очередь до page:ready)."""
        from github_wallpaper.wallpaper.bridge import serialize_message

        if self._window is None:
            return

        json_str = serialize_message(message)
        page = self._window.web_view.page()

        if self._message_queue.should_queue():
            self._message_queue.enqueue(json_str)
            return

        deliver_json_to_page(page, json_str)

    def _flush_pending_messages(self) -> None:
        if self._window is None:
            return

        page = self._window.web_view.page()
        for json_str in self._message_queue.flush():
            deliver_json_to_page(page, json_str)

    def _post_wallpaper_command(self, command: str) -> None:
        self.post_message_as_json({"type": command})

    def _post_viewport_update(self, screen: QScreen) -> None:
        if not self._applied:
            return

        geometry = screen.geometry()
        available = screen.availableGeometry()
        self.post_message_as_json(
            {
                "type": "viewport:update",
                "payload": {
                    "width": geometry.width(),
                    "height": geometry.height(),
                    "safeTop": max(0, available.top() - geometry.top()),
                    "safeRight": max(0, geometry.right() - available.right()),
                    "safeBottom": max(0, geometry.bottom() - available.bottom()),
                    "safeLeft": max(0, available.left() - geometry.left()),
                },
            }
        )

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
        self._post_viewport_update(screen)

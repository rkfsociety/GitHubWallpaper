"""QApplication и иконка в системном трее."""

from __future__ import annotations

import logging
import sys

from PySide6.QtCore import QTimer, QSize
from PySide6.QtGui import QAction, QColor, QIcon, QPainter, QPixmap
from PySide6.QtWidgets import QApplication, QMenu, QMessageBox, QSystemTrayIcon

from github_wallpaper import autostart
from github_wallpaper.desktop.auto_pause_monitor import AutoPauseMonitor
from github_wallpaper.github.github_session import GitHubSession
from github_wallpaper.github.repo_poller import RepoPoller
from github_wallpaper.settings.settings_dialog import SettingsDialog
from github_wallpaper.settings_store import SettingsStore
from github_wallpaper.wallpaper.bridge import WallpaperBridge
from github_wallpaper.wallpaper.controller import WallpaperController
from github_wallpaper.wallpaper.pause_coordinator import WallpaperPauseCoordinator

APP_DISPLAY_NAME = "GitHub Wallpaper"
_logger = logging.getLogger(__name__)


def create_tray_icon() -> QIcon:
    """Простая иконка в стиле C# AppIcons (тёмный круг + синий акцент)."""
    size = 32
    pixmap = QPixmap(QSize(size, size))
    pixmap.fill(QColor(0, 0, 0, 0))

    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.RenderHint.Antialiasing)
    painter.setBrush(QColor(36, 41, 46))
    painter.setPen(QColor(0, 0, 0, 0))
    painter.drawEllipse(2, 2, size - 4, size - 4)
    painter.setBrush(QColor(88, 166, 255))
    painter.drawEllipse(10, 10, 12, 12)
    painter.end()

    return QIcon(pixmap)


class GitHubWallpaperApp:
    """Каркас приложения: трей, обои (WebEngine) и меню Настройки / Пауза / Выход."""

    def __init__(self) -> None:
        self._app = QApplication(sys.argv)
        self._app.setQuitOnLastWindowClosed(False)
        self._app.setApplicationName(APP_DISPLAY_NAME)
        self._app.setOrganizationName("rkfsociety")
        self._app.aboutToQuit.connect(self._on_about_to_quit)

        self._settings_store = SettingsStore()
        settings = self._settings_store.load()

        self._github_session = GitHubSession()
        self._repo_poller = RepoPoller(self._github_session.client)
        self._wallpaper = WallpaperController()
        self._pause_coordinator = WallpaperPauseCoordinator(self._wallpaper)
        self._auto_pause_monitor = AutoPauseMonitor(self._pause_coordinator)
        self._bridge = WallpaperBridge(
            self._wallpaper,
            github_session=self._github_session,
            repo_poller=self._repo_poller,
            settings_loader=self._settings_store.load,
        )
        self._wallpaper.set_bridge(self._bridge)
        self._wallpaper.set_polling_pause_handler(self._repo_poller.set_paused)
        self._bridge.start()

        self._repo_poller.configure_poll_intervals(settings.poll_interval_preset)
        self._repo_poller.configure_card_display(settings.card_display)
        self._wallpaper.configure_display(settings.display_device_name)
        autostart.refresh_path_if_enabled()
        self._auto_pause_monitor.configure(settings)

        self._settings_dialog: SettingsDialog | None = None

        self._pause_action = QAction("Пауза", self._app)
        self._pause_action.triggered.connect(self._on_pause)

        menu = QMenu()
        menu.addAction("Настройки", self._on_settings)
        menu.addAction(self._pause_action)
        menu.addSeparator()
        menu.addAction("Выход", self._app.quit)

        self._tray = QSystemTrayIcon(create_tray_icon(), self._app)
        self._tray.setToolTip(APP_DISPLAY_NAME)
        self._tray.setContextMenu(menu)
        self._tray.activated.connect(self._on_tray_activated)

    def run(self) -> int:
        if not QSystemTrayIcon.isSystemTrayAvailable():
            QMessageBox.critical(
                None,
                APP_DISPLAY_NAME,
                "Системный трей недоступен на этой платформе.",
            )
            return 1

        self._tray.show()
        QTimer.singleShot(0, self._apply_wallpaper)
        return self._app.exec()

    def _apply_wallpaper(self) -> None:
        try:
            settings = self._settings_store.load()
            self._wallpaper.apply()
            self._repo_poller.start(settings.load_repositories())
            self._update_pause_action()
        except Exception as ex:
            _logger.exception("Не удалось запустить обои")
            QMessageBox.critical(
                None,
                APP_DISPLAY_NAME,
                f"Не удалось запустить обои:\n{ex}",
            )
            self._app.quit()

    def _on_about_to_quit(self) -> None:
        self._auto_pause_monitor.dispose()
        self._bridge.dispose()
        self._repo_poller.dispose()
        self._github_session.dispose()
        self._wallpaper.dispose()

    def _on_tray_activated(self, reason: QSystemTrayIcon.ActivationReason) -> None:
        if reason == QSystemTrayIcon.ActivationReason.DoubleClick:
            self._on_settings()

    def _on_settings(self) -> None:
        if self._settings_dialog is not None and self._settings_dialog.isVisible():
            self._settings_dialog.raise_()
            self._settings_dialog.activateWindow()
            return

        self._settings_dialog = SettingsDialog(
            github_session=self._github_session,
            settings_store=self._settings_store,
            repo_poller=self._repo_poller,
            auto_pause_monitor=self._auto_pause_monitor,
            wallpaper_controller=self._wallpaper,
            bridge=self._bridge,
        )
        self._settings_dialog.show()

    def _on_pause(self) -> None:
        if not self._wallpaper.is_applied:
            return
        self._pause_coordinator.toggle_user_pause()
        self._update_pause_action()

    def _update_pause_action(self) -> None:
        self._pause_action.setText("Возобновить" if self._wallpaper.is_paused else "Пауза")
        self._pause_action.setEnabled(self._wallpaper.is_applied)

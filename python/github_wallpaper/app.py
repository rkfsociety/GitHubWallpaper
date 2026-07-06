"""QApplication и иконка в системном трее."""

from __future__ import annotations

import logging
import sys

from PySide6.QtCore import QTimer, QSize
from PySide6.QtGui import QAction, QColor, QIcon, QPainter, QPixmap
from PySide6.QtWidgets import QApplication, QMenu, QMessageBox, QSystemTrayIcon

from github_wallpaper.settings_store import AppSettings
from github_wallpaper.wallpaper.bridge import WallpaperBridge
from github_wallpaper.wallpaper.controller import WallpaperController

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

        self._paused = False
        self._wallpaper = WallpaperController()
        self._bridge = WallpaperBridge(self._wallpaper)
        self._wallpaper.set_bridge(self._bridge)
        self._bridge.start()

        settings = AppSettings.load()
        self._wallpaper.configure_display(settings.display_device_name)

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
            self._wallpaper.apply()
        except Exception as ex:
            _logger.exception("Не удалось запустить обои")
            QMessageBox.critical(
                None,
                APP_DISPLAY_NAME,
                f"Не удалось запустить обои:\n{ex}",
            )
            self._app.quit()

    def _on_about_to_quit(self) -> None:
        self._bridge.dispose()
        self._wallpaper.dispose()

    def _on_tray_activated(self, reason: QSystemTrayIcon.ActivationReason) -> None:
        if reason == QSystemTrayIcon.ActivationReason.DoubleClick:
            self._on_settings()

    def _on_settings(self) -> None:
        QMessageBox.information(
            None,
            APP_DISPLAY_NAME,
            "Окно настроек будет реализовано на этапе 6.5.",
        )

    def _on_pause(self) -> None:
        self._paused = not self._paused
        if self._paused:
            self._wallpaper.pause()
        else:
            self._wallpaper.resume()
        self._update_pause_action()

    def _update_pause_action(self) -> None:
        self._pause_action.setText("Возобновить" if self._paused else "Пауза")

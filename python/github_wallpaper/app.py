"""QApplication и иконка в системном трее."""

from __future__ import annotations

import logging
import sys

from PySide6.QtCore import Qt, QThread, QTimer, QSize, Signal
from PySide6.QtGui import QAction, QColor, QIcon, QPainter, QPixmap
from PySide6.QtWidgets import QApplication, QMenu, QMessageBox, QSystemTrayIcon

from github_wallpaper import autostart
from github_wallpaper.desktop.auto_pause_monitor import AutoPauseMonitor
from github_wallpaper.github.github_session import GitHubSession
from github_wallpaper.github.repo_poller import RepoPoller
from github_wallpaper.settings.settings_dialog import SettingsDialog
from github_wallpaper.settings_store import SettingsStore
from github_wallpaper.update import installer as update_installer
from github_wallpaper.update.models import (
    AppUpdateCheckResult,
    AppUpdateDownloadProgress,
    AppUpdateInfo,
    Failed,
    Skipped,
    UpToDate,
    UpdateAvailable,
)
from github_wallpaper.update.progress_dialog import UpdateProgressDialog
from github_wallpaper.update.service import AppUpdateService
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


class _UpdateCheckWorker(QThread):
    finished_with_result = Signal(object)

    def __init__(self, service: AppUpdateService) -> None:
        super().__init__()
        self._service = service

    def run(self) -> None:
        self.finished_with_result.emit(self._service.check_for_updates())


class _UpdateDownloadWorker(QThread):
    progress_changed = Signal(object)
    finished_success = Signal()
    finished_error = Signal(str)

    def __init__(self, service: AppUpdateService, update: AppUpdateInfo) -> None:
        super().__init__()
        self._service = service
        self._update = update

    def run(self) -> None:
        try:
            self._service.download_and_apply(
                self._update,
                progress=lambda value: self.progress_changed.emit(value),
            )
            self.finished_success.emit()
        except Exception as ex:
            self.finished_error.emit(update_installer.format_download_error(ex))


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
        self._app_update_service = AppUpdateService(self._settings_store)
        self._update_check_worker: _UpdateCheckWorker | None = None
        self._update_download_worker: _UpdateDownloadWorker | None = None

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
        self._check_updates_action = menu.addAction(
            "Проверить обновления…",
            self._on_check_for_updates,
        )
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
        QTimer.singleShot(5_000, self._check_for_updates_automatically)
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
        self._app_update_service.close()

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

    def _check_for_updates_automatically(self) -> None:
        if not self._app_update_service.should_check_automatically():
            return

        worker = _UpdateCheckWorker(self._app_update_service)
        worker.finished_with_result.connect(self._on_automatic_update_check_finished)
        worker.finished.connect(worker.deleteLater)
        self._update_check_worker = worker
        worker.start()

    def _on_automatic_update_check_finished(self, result: AppUpdateCheckResult) -> None:
        self._app_update_service.record_automatic_check()
        if isinstance(result, UpdateAvailable):
            self._notify_update_available(result.update.version)

    def _notify_update_available(self, version: str) -> None:
        self._tray.showMessage(
            APP_DISPLAY_NAME,
            f"Доступна версия {version}. ПКМ по иконке → «Проверить обновления…».",
            QSystemTrayIcon.MessageIcon.Information,
            8_000,
        )

    def _on_check_for_updates(self) -> None:
        if self._update_check_worker is not None and self._update_check_worker.isRunning():
            return

        self._check_updates_action.setEnabled(False)
        worker = _UpdateCheckWorker(self._app_update_service)
        worker.finished_with_result.connect(self._on_manual_update_check_finished)
        worker.finished.connect(lambda: self._check_updates_action.setEnabled(True))
        worker.finished.connect(worker.deleteLater)
        self._update_check_worker = worker
        worker.start()

    def _on_manual_update_check_finished(self, result: AppUpdateCheckResult) -> None:
        if isinstance(result, UpToDate):
            QMessageBox.information(
                None,
                APP_DISPLAY_NAME,
                f"Установлена последняя версия ({result.current_version}).",
            )
            return

        if isinstance(result, UpdateAvailable):
            self._prompt_and_apply_update(result.update)
            return

        if isinstance(result, Skipped):
            QMessageBox.information(None, APP_DISPLAY_NAME, result.reason)
            return

        if isinstance(result, Failed):
            QMessageBox.warning(None, APP_DISPLAY_NAME, result.message)

    def _prompt_and_apply_update(self, update: AppUpdateInfo) -> None:
        answer = QMessageBox.question(
            None,
            APP_DISPLAY_NAME,
            f"Доступна новая версия {update.version}.\n\n"
            "Скачать и перезапустить приложение сейчас?",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
            QMessageBox.StandardButton.No,
        )
        if answer != QMessageBox.StandardButton.Yes:
            return

        progress_dialog = UpdateProgressDialog(update.version)
        progress_dialog.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        progress_dialog.show()

        worker = _UpdateDownloadWorker(self._app_update_service, update)
        worker.progress_changed.connect(progress_dialog.report)
        worker.finished_success.connect(self._app.quit)
        worker.finished_error.connect(
            lambda message: self._show_update_error(progress_dialog, update, message),
        )
        worker.finished.connect(progress_dialog.close)
        worker.finished.connect(worker.deleteLater)
        self._update_download_worker = worker
        worker.start()

    def _show_update_error(
        self,
        progress_dialog: UpdateProgressDialog,
        update: AppUpdateInfo,
        message: str,
    ) -> None:
        progress_dialog.close()
        QMessageBox.critical(
            None,
            APP_DISPLAY_NAME,
            f"Не удалось установить обновление:\n{message}\n\n"
            f"Можно скачать вручную:\n{update.release_page_url}",
        )


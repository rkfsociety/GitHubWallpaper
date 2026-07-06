"""Точка входа GitHub Wallpaper v2.0."""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication, QMessageBox

from github_wallpaper.app import APP_DISPLAY_NAME, GitHubWallpaperApp
from github_wallpaper.paths import ensure_wallpaper_assets
from github_wallpaper.single_instance import SingleInstanceGuard


def main() -> int:
    guard = SingleInstanceGuard()
    if not guard.try_acquire():
        _show_already_running_message()
        return 0

    try:
        ensure_wallpaper_assets()
        app = GitHubWallpaperApp()
        return app.run()
    finally:
        guard.release()


def _show_already_running_message() -> None:
    # Минимальный QApplication нужен для QMessageBox до основного цикла.
    qt_app = QApplication.instance() or QApplication(sys.argv)
    QMessageBox.information(
        None,
        APP_DISPLAY_NAME,
        "GitHub Wallpaper уже запущен.\n\n"
        "Иконка приложения — в области уведомлений (трей).",
    )
    if qt_app:
        del qt_app


if __name__ == "__main__":
    raise SystemExit(main())

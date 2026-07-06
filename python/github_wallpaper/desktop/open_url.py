"""Открытие URL в браузере (Linux root / headless fallback)."""

from __future__ import annotations

import logging
import os
import subprocess
import sys

from PySide6.QtCore import QUrl
from PySide6.QtGui import QDesktopServices, QGuiApplication

_logger = logging.getLogger(__name__)


def open_url(url: str) -> bool:
    """Открывает URL в браузере. При неудаче копирует ссылку в буфер обмена."""
    trimmed = url.strip()
    if not trimmed:
        return False

    if QDesktopServices.openUrl(QUrl(trimmed)):
        return True

    if sys.platform.startswith("linux"):
        display = os.environ.get("DISPLAY", ":0")
        env = {**os.environ, "DISPLAY": display}
        for command in (
            ["xdg-open", trimmed],
            ["sensible-browser", trimmed],
            ["x-www-browser", trimmed],
        ):
            try:
                subprocess.Popen(
                    command,
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                    env=env,
                    start_new_session=True,
                )
                return True
            except OSError:
                continue

    clipboard = QGuiApplication.clipboard()
    if clipboard is not None:
        clipboard.setText(trimmed)

    _logger.warning("Не удалось открыть браузер, URL скопирован в буфер: %s", trimmed)
    return False

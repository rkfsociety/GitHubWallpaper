"""Диалог скачивания обновления."""

from __future__ import annotations

from PySide6.QtWidgets import QLabel, QProgressBar, QVBoxLayout, QWidget

from github_wallpaper.update.models import AppUpdateDownloadProgress


class UpdateProgressDialog(QWidget):
    """Небольшое окно прогресса загрузки обновления."""

    def __init__(self, version: str, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setWindowTitle("GitHub Wallpaper — обновление")
        self.setFixedSize(420, 110)

        title = QLabel(f"Скачивание версии {version}…")
        self._status_label = QLabel("Подключение к GitHub…")
        self._status_label.setStyleSheet("color: gray;")
        self._progress_bar = QProgressBar()
        self._progress_bar.setRange(0, 100)
        self._progress_bar.setValue(0)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(16, 16, 16, 16)
        layout.addWidget(title)
        layout.addWidget(self._status_label)
        layout.addWidget(self._progress_bar)

    def report(self, progress: AppUpdateDownloadProgress) -> None:
        if progress.percent is not None:
            self._progress_bar.setRange(0, 100)
            self._progress_bar.setValue(progress.percent)
            total = progress.total_bytes or 0
            self._status_label.setText(
                f"Загружено {_format_size(progress.bytes_received)} "
                f"из {_format_size(total)}",
            )
            return

        self._progress_bar.setRange(0, 0)
        self._status_label.setText(f"Загружено {_format_size(progress.bytes_received)}…")


def _format_size(value: int) -> str:
    if value >= 1_000_000:
        return f"{value / 1_000_000:.1f} МБ"
    if value >= 1_000:
        return f"{value / 1_000:.0f} КБ"
    return f"{value} Б"

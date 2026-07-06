"""Borderless QWebEngineView на весь выбранный экран."""

from __future__ import annotations

from PySide6.QtCore import Qt, QUrl
from PySide6.QtGui import QScreen
from PySide6.QtWebEngineCore import QWebEnginePage, QWebEngineSettings
from PySide6.QtWebEngineWidgets import QWebEngineView
from PySide6.QtWidgets import QVBoxLayout, QWidget


class WallpaperWindow(QWidget):
    """Окно обоев: frameless WebEngine на геометрии целевого экрана."""

    def __init__(self, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self._attached = False

        self.setWindowFlags(
            Qt.WindowType.FramelessWindowHint
            | Qt.WindowType.WindowStaysOnBottomHint
            | Qt.WindowType.Tool
            | Qt.WindowType.WindowDoesNotAcceptFocus
        )
        self.setAttribute(Qt.WidgetAttribute.WA_ShowWithoutActivating, True)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground, False)

        self._view = QWebEngineView(self)
        self._configure_web_engine(self._view.page())

        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.addWidget(self._view)

    @property
    def web_view(self) -> QWebEngineView:
        return self._view

    @property
    def is_attached(self) -> bool:
        return self._attached

    def mark_attached(self, attached: bool) -> None:
        self._attached = attached

    def load_url(self, url: QUrl) -> None:
        self._view.load(url)

    def fit_to_screen(self, screen: QScreen) -> None:
        self.setGeometry(screen.geometry())

    def set_render_visible(self, visible: bool) -> None:
        """Пауза/возобновление рендера: скрывает WebEngineView."""
        self._view.setVisible(visible)

    @staticmethod
    def _configure_web_engine(page: QWebEnginePage) -> None:
        settings = page.settings()
        settings.setAttribute(QWebEngineSettings.WebAttribute.JavascriptEnabled, True)
        settings.setAttribute(QWebEngineSettings.WebAttribute.LocalContentCanAccessFileUrls, True)
        settings.setAttribute(QWebEngineSettings.WebAttribute.LocalContentCanAccessRemoteUrls, True)
        settings.setAttribute(QWebEngineSettings.WebAttribute.ErrorPageEnabled, False)
        settings.setAttribute(QWebEngineSettings.WebAttribute.FullScreenSupportEnabled, False)
        settings.setAttribute(QWebEngineSettings.WebAttribute.PlaybackRequiresUserGesture, True)

        page.setBackgroundColor(Qt.GlobalColor.transparent)

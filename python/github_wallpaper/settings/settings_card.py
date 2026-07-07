"""Карточка секции настроек."""

from __future__ import annotations

from PySide6.QtWidgets import QFrame, QLabel, QVBoxLayout, QWidget


class SettingsCard(QFrame):
    """Секция с заголовком и телом (аналог SettingsCard.cs)."""

    def __init__(self, title: str, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setObjectName("settingsCard")

        root = QVBoxLayout(self)
        root.setContentsMargins(16, 14, 16, 14)
        root.setSpacing(10)

        self._title_label = QLabel(title)
        self._title_label.setObjectName("cardTitle")
        root.addWidget(self._title_label)

        self.body = QWidget()
        body_layout = QVBoxLayout(self.body)
        body_layout.setContentsMargins(0, 0, 0, 0)
        body_layout.setSpacing(8)
        root.addWidget(self.body)

    def set_title_visible(self, visible: bool) -> None:
        self._title_label.setVisible(visible)

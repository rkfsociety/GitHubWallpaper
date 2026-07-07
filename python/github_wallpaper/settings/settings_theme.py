"""Тёмная тема окна настроек (палитра SettingsTheme.cs)."""

from __future__ import annotations

from PySide6.QtWidgets import QWidget

_SETTINGS_QSS = """
QDialog#settingsDialog {
    background-color: #0a0c14;
    color: #f0f4fc;
}

QLabel#cardTitle {
    color: #f0f4fc;
    font-size: 11pt;
    font-weight: 600;
    padding-bottom: 2px;
}

QLabel#mutedLabel {
    color: #949cac;
    font-size: 8.75pt;
}

QFrame#settingsCard {
    background-color: #181c28;
    border: 1px solid #343a48;
    border-radius: 14px;
}

QGroupBox {
    background-color: #181c28;
    border: 1px solid #343a48;
    border-radius: 14px;
    margin-top: 12px;
    padding: 14px;
    font-weight: 600;
    color: #f0f4fc;
}

QGroupBox::title {
    subcontrol-origin: margin;
    left: 12px;
    padding: 0 6px;
}

QLineEdit, QComboBox, QSpinBox {
    background-color: #0c0e16;
    color: #f0f4fc;
    border: 1px solid #343a48;
    border-radius: 8px;
    padding: 6px 10px;
    min-height: 28px;
}

QLineEdit:focus, QComboBox:focus, QSpinBox:focus {
    border-color: #58a6ff;
}

QComboBox::drop-down {
    border: none;
    width: 22px;
}

QComboBox QAbstractItemView {
    background-color: #181c28;
    color: #f0f4fc;
    selection-background-color: #30384e;
    border: 1px solid #343a48;
}

QPushButton#accentButton {
    background-color: #ff8c00;
    color: white;
    border: none;
    border-radius: 8px;
    padding: 6px 14px;
    min-height: 30px;
    font-weight: 600;
}

QPushButton#accentButton:hover {
    background-color: #ffa82e;
}

QPushButton#accentButton:pressed {
    background-color: #dc7600;
}

QPushButton#ghostButton {
    background-color: #181c28;
    color: #f0f4fc;
    border: 1px solid #343a48;
    border-radius: 8px;
    padding: 6px 14px;
    min-height: 30px;
}

QPushButton#ghostButton:hover {
    border-color: #58a6ff;
    color: #79c0ff;
}

QPushButton#outlineButton {
    background-color: transparent;
    color: #a855f7;
    border: 1px solid #a855f7;
    border-radius: 8px;
    padding: 6px 14px;
    min-height: 30px;
}

QPushButton#outlineButton:hover {
    background-color: rgba(168, 85, 247, 0.12);
}

QCheckBox, QRadioButton {
    color: #f0f4fc;
    spacing: 8px;
}

QCheckBox::indicator, QRadioButton::indicator {
    width: 16px;
    height: 16px;
}

QCheckBox::indicator:unchecked, QRadioButton::indicator:unchecked {
    border: 1px solid #343a48;
    background: #0c0e16;
    border-radius: 4px;
}

QRadioButton::indicator:unchecked {
    border-radius: 8px;
}

QCheckBox::indicator:checked {
    border: 1px solid #ff8c00;
    background: #ff8c00;
    border-radius: 4px;
}

QRadioButton::indicator:checked {
    border: 3px solid #ff8c00;
    background: #0c0e16;
    border-radius: 8px;
}

QTableWidget {
    background-color: #0c0e16;
    color: #f0f4fc;
    border: 1px solid #343a48;
    border-radius: 8px;
    gridline-color: #2a3040;
    selection-background-color: #30384e;
    selection-color: #f0f4fc;
}

QHeaderView::section {
    background-color: #10131c;
    color: #949cac;
    border: none;
    border-bottom: 1px solid #343a48;
    padding: 4px;
}
"""


def apply_settings_theme(root: QWidget) -> None:
    """Применяет QSS к диалогу и дочерним виджетам."""
    root.setObjectName("settingsDialog")
    root.setStyleSheet(_SETTINGS_QSS)


def style_accent_button(button: QWidget) -> None:
    button.setObjectName("accentButton")


def style_ghost_button(button: QWidget) -> None:
    button.setObjectName("ghostButton")


def style_outline_button(button: QWidget) -> None:
    button.setObjectName("outlineButton")


def style_muted_label(label: QWidget) -> None:
    label.setObjectName("mutedLabel")

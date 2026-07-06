"""Разрешение монитора по DisplayDeviceName (совместимость с C# Screen.DeviceName)."""

from __future__ import annotations

from PySide6.QtGui import QGuiApplication, QScreen


class ScreenHelper:
    """Список экранов и поиск по имени устройства."""

    @staticmethod
    def all_screens() -> list[QScreen]:
        app = QGuiApplication.instance()
        if app is None:
            return []
        return list(app.screens())

    @staticmethod
    def primary() -> QScreen:
        app = QGuiApplication.instance()
        if app is None:
            raise RuntimeError("QGuiApplication не инициализирован.")

        screen = app.primaryScreen()
        if screen is not None:
            return screen

        screens = ScreenHelper.all_screens()
        if not screens:
            raise RuntimeError("Нет доступных экранов.")
        return screens[0]

    @staticmethod
    def resolve(device_name: str | None) -> QScreen:
        """
        Находит экран по имени (QScreen.name() ≈ Screen.DeviceName на Windows).
        Пустое имя — основной монитор.
        """
        if not device_name or not device_name.strip():
            return ScreenHelper.primary()

        normalized = device_name.strip()
        for screen in ScreenHelper.all_screens():
            if screen.name().lower() == normalized.lower():
                return screen

        return ScreenHelper.primary()

    @staticmethod
    def format_label(screen: QScreen, index: int) -> str:
        geo = screen.geometry()
        primary = " (основной)" if screen == ScreenHelper.primary() else ""
        return f"{index + 1}: {geo.width()}×{geo.height()}{primary}"

    @staticmethod
    def device_name(screen: QScreen) -> str:
        """Имя устройства для settings.json (displayDeviceName)."""
        return screen.name()

"""Разрешение монитора по DisplayDeviceName."""

from __future__ import annotations

import sys
from typing import TYPE_CHECKING

from PySide6.QtCore import QRect
from PySide6.QtGui import QGuiApplication, QScreen

if TYPE_CHECKING:
    from collections.abc import Mapping


def _screen_rect_key(rect: QRect) -> tuple[int, int, int, int]:
    return rect.x(), rect.y(), rect.width(), rect.height()


def _win32_monitors_by_geometry() -> Mapping[tuple[int, int, int, int], str]:
    """Win32 EnumDisplayMonitors: (x, y, w, h) → \\\\.\\DISPLAYn."""
    if sys.platform != "win32":
        return {}

    import ctypes
    from ctypes import wintypes

    user32 = ctypes.windll.user32

    class RECT(ctypes.Structure):
        _fields_ = [
            ("left", ctypes.c_long),
            ("top", ctypes.c_long),
            ("right", ctypes.c_long),
            ("bottom", ctypes.c_long),
        ]

    class MONITORINFOEXW(ctypes.Structure):
        _fields_ = [
            ("cbSize", wintypes.DWORD),
            ("rcMonitor", RECT),
            ("rcWork", RECT),
            ("dwFlags", wintypes.DWORD),
            ("szDevice", wintypes.WCHAR * 32),
        ]

    monitors: dict[tuple[int, int, int, int], str] = {}

    def callback(h_monitor, _hdc, _rect, _lparam) -> int:
        info = MONITORINFOEXW()
        info.cbSize = ctypes.sizeof(MONITORINFOEXW)
        if not user32.GetMonitorInfoW(h_monitor, ctypes.byref(info)):
            return 1

        bounds = info.rcMonitor
        key = (
            bounds.left,
            bounds.top,
            bounds.right - bounds.left,
            bounds.bottom - bounds.top,
        )
        monitors[key] = info.szDevice
        return 1

    MONITORENUMPROC = ctypes.WINFUNCTYPE(
        ctypes.c_bool,
        ctypes.c_ulong,
        ctypes.c_ulong,
        ctypes.POINTER(RECT),
        ctypes.c_double,
    )
    user32.EnumDisplayMonitors(0, 0, MONITORENUMPROC(callback), 0)
    return monitors


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
    def win32_device_name(screen: QScreen) -> str | None:
        """Win32 DisplayDeviceName (\\\\.\\DISPLAYn) для того же монитора."""
        return _win32_monitors_by_geometry().get(_screen_rect_key(screen.geometry()))

    @staticmethod
    def resolve(device_name: str | None) -> QScreen:
        """
        Находит экран по имени (QScreen.name() или Win32 DisplayDeviceName).
        Пустое имя — основной монитор.
        """
        if not device_name or not device_name.strip():
            return ScreenHelper.primary()

        normalized = device_name.strip()
        for screen in ScreenHelper.all_screens():
            if screen.name().lower() == normalized.lower():
                return screen

            win32_name = ScreenHelper.win32_device_name(screen)
            if win32_name and win32_name.lower() == normalized.lower():
                return screen

        return ScreenHelper.primary()

    @staticmethod
    def format_label(screen: QScreen, index: int) -> str:
        geo = screen.geometry()
        primary = ScreenHelper.primary()
        position = ScreenHelper._relative_position(screen, primary)
        primary_mark = " (основной)" if screen == primary else ""
        return f"{index + 1}: {geo.width()}×{geo.height()}{position}{primary_mark}"

    @staticmethod
    def device_name(screen: QScreen) -> str:
        """Имя устройства для settings.json (displayDeviceName)."""
        return screen.name()

    @staticmethod
    def _relative_position(screen: QScreen, primary: QScreen) -> str:
        if screen == primary:
            return ""

        geo = screen.geometry()
        primary_geo = primary.geometry()

        if geo.right() <= primary_geo.left():
            return ", слева"
        if geo.left() >= primary_geo.right():
            return ", справа"
        if geo.bottom() <= primary_geo.top():
            return ", сверху"
        if geo.top() >= primary_geo.bottom():
            return ", снизу"
        return ""

"""Автопауза при полноэкранном окне и работе от батареи (AutoPauseMonitor.cs)."""

from __future__ import annotations

import ctypes
import logging
import sys
from pathlib import Path
from typing import TYPE_CHECKING

from PySide6.QtCore import QTimer

from github_wallpaper.settings_store import AppSettings

if TYPE_CHECKING:
    from github_wallpaper.wallpaper.pause_coordinator import WallpaperPauseCoordinator

_logger = logging.getLogger(__name__)
_FULLSCREEN_TOLERANCE_PX = 8


class AutoPauseMonitor:
    """Периодически проверяет условия автопаузы."""

    def __init__(self, pause_coordinator: WallpaperPauseCoordinator) -> None:
        self._pause_coordinator = pause_coordinator
        self._pause_on_fullscreen = True
        self._pause_on_battery = True
        self._timer = QTimer()
        self._timer.setInterval(1000)
        self._timer.timeout.connect(self._evaluate_conditions)

    def configure(self, settings: AppSettings) -> None:
        self._pause_on_fullscreen = settings.pause_on_fullscreen
        self._pause_on_battery = settings.pause_on_battery

        if self._pause_on_fullscreen or self._pause_on_battery:
            if not self._timer.isActive():
                self._timer.start()
            self._evaluate_conditions()
        else:
            self._timer.stop()
            self._pause_coordinator.reset_auto_pause()

    def dispose(self) -> None:
        self._timer.stop()

    def _evaluate_conditions(self) -> None:
        if self._pause_on_fullscreen:
            self._pause_coordinator.set_fullscreen_paused(_is_foreground_fullscreen())
        else:
            self._pause_coordinator.set_fullscreen_paused(False)

        if self._pause_on_battery:
            self._pause_coordinator.set_battery_paused(_is_on_battery())
        else:
            self._pause_coordinator.set_battery_paused(False)


def _is_on_battery() -> bool:
    if sys.platform == "win32":
        return _windows_on_battery()
    return _linux_on_battery()


def _is_foreground_fullscreen() -> bool:
    if sys.platform == "win32":
        return _windows_foreground_fullscreen()
    if sys.platform.startswith("linux"):
        return _linux_foreground_fullscreen()
    return False


def _windows_on_battery() -> bool:
    class SystemPowerStatus(ctypes.Structure):
        _fields_ = [
            ("ac_line_status", ctypes.c_byte),
            ("battery_flag", ctypes.c_byte),
            ("battery_life_percent", ctypes.c_byte),
            ("system_status_flag", ctypes.c_byte),
            ("battery_life_time", ctypes.c_ulong),
            ("battery_full_life_time", ctypes.c_ulong),
        ]

    status = SystemPowerStatus()
    if not ctypes.windll.kernel32.GetSystemPowerStatus(ctypes.byref(status)):
        return False
    return status.ac_line_status == 0


def _windows_foreground_fullscreen() -> bool:
    from ctypes import wintypes

    user32 = ctypes.windll.user32

    hwnd = user32.GetForegroundWindow()
    if not hwnd:
        return False

    process_id = wintypes.DWORD()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(process_id))
    if process_id.value == ctypes.windll.kernel32.GetCurrentProcessId():
        return False

    class_name = ctypes.create_unicode_buffer(256)
    if user32.GetClassNameW(hwnd, class_name, 256) <= 0:
        return False
    if class_name.value in {"Progman", "WorkerW", "Shell_TrayWnd"}:
        return False

    rect = wintypes.RECT()
    if not user32.GetWindowRect(hwnd, ctypes.byref(rect)):
        return False

    monitor = user32.MonitorFromWindow(hwnd, 2)
    if not monitor:
        return False

    class MonitorInfo(ctypes.Structure):
        _fields_ = [
            ("cbSize", wintypes.DWORD),
            ("rcMonitor", wintypes.RECT),
            ("rcWork", wintypes.RECT),
            ("dwFlags", wintypes.DWORD),
        ]

    monitor_info = MonitorInfo()
    monitor_info.cbSize = ctypes.sizeof(MonitorInfo)
    if not user32.GetMonitorInfoW(monitor, ctypes.byref(monitor_info)):
        return False

    mon = monitor_info.rcMonitor
    tolerance = _FULLSCREEN_TOLERANCE_PX
    return (
        rect.left <= mon.left + tolerance
        and rect.top <= mon.top + tolerance
        and rect.right >= mon.right - tolerance
        and rect.bottom >= mon.bottom - tolerance
    )


def _linux_on_battery() -> bool:
    power_supply = Path("/sys/class/power_supply")
    if not power_supply.is_dir():
        return False

    for supply in power_supply.iterdir():
        status_file = supply / "status"
        type_file = supply / "type"
        if not status_file.is_file() or not type_file.is_file():
            continue
        try:
            supply_type = type_file.read_text(encoding="utf-8").strip().lower()
            status = status_file.read_text(encoding="utf-8").strip().lower()
        except OSError:
            continue
        if supply_type == "battery" and status == "discharging":
            return True
    return False


def _linux_foreground_fullscreen() -> bool:
    try:
        from ctypes import CDLL, POINTER, c_int, c_long, c_ulong, c_void_p
        from ctypes.util import find_library
    except ImportError:
        return False

    x11_name = find_library("X11")
    if not x11_name:
        return False

    x11 = CDLL(x11_name)
    x11.XOpenDisplay.restype = c_void_p
    x11.XDefaultRootWindow.argtypes = [c_void_p]
    x11.XDefaultRootWindow.restype = c_ulong
    x11.XInternAtom.argtypes = [c_void_p, ctypes.c_char_p, c_int]
    x11.XInternAtom.restype = c_ulong
    x11.XGetInputFocus.argtypes = [c_void_p, POINTER(c_ulong), POINTER(c_int)]
    x11.XGetWindowProperty.argtypes = [
        c_void_p,
        c_ulong,
        c_ulong,
        c_long,
        c_long,
        c_int,
        c_ulong,
        POINTER(c_int),
        POINTER(c_ulong),
        POINTER(c_ulong),
        POINTER(c_ulong),
        POINTER(POINTER(c_ulong)),
    ]

    display = x11.XOpenDisplay(None)
    if not display:
        return False

    try:
        focused = c_ulong()
        revert = c_int()
        x11.XGetInputFocus(display, ctypes.byref(focused), ctypes.byref(revert))

        window_id = focused.value
        root = x11.XDefaultRootWindow(display)
        if window_id in {0, root}:
            return False

        net_wm_state = x11.XInternAtom(display, b"_NET_WM_STATE", False)
        fullscreen_atom = x11.XInternAtom(display, b"_NET_WM_STATE_FULLSCREEN", False)

        actual_type = c_ulong()
        actual_format = c_int()
        nitems = c_ulong()
        bytes_after = c_ulong()
        prop = POINTER(c_ulong)()

        result = x11.XGetWindowProperty(
            display,
            window_id,
            net_wm_state,
            0,
            1024,
            0,
            4,
            ctypes.byref(actual_type),
            ctypes.byref(actual_format),
            ctypes.byref(nitems),
            ctypes.byref(bytes_after),
            ctypes.byref(prop),
        )
        if result != 0 or not prop:
            return False

        atoms = [prop[index] for index in range(int(nitems.value))]
        x11.XFree(prop)
        return fullscreen_atom in atoms
    except Exception:
        _logger.debug("Не удалось определить полноэкранное окно через X11", exc_info=True)
        return False
    finally:
        x11.XCloseDisplay(display)

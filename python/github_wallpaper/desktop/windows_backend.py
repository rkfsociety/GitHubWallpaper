"""Windows: встраивание в WorkerW через ctypes."""

from __future__ import annotations

import ctypes
import sys
from dataclasses import dataclass
from ctypes import wintypes

from PySide6.QtCore import Qt, QRect
from PySide6.QtGui import QGuiApplication, QScreen
from PySide6.QtWidgets import QWidget

from github_wallpaper.desktop.backend import DesktopBackend

if sys.platform != "win32":
    raise ImportError("windows_backend доступен только на Windows.")

GWL_STYLE = -16
GWL_EXSTYLE = -20

WS_CHILD = 0x4000_0000
WS_VISIBLE = 0x1000_0000
WS_CAPTION = 0x00C0_0000
WS_THICKFRAME = 0x0004_0000
WS_MINIMIZEBOX = 0x0002_0000
WS_MAXIMIZEBOX = 0x0001_0000
WS_SYSMENU = 0x0008_0000
WS_BORDER = 0x0080_0000
WS_POPUP = 0x8000_0000

WS_EX_NOACTIVATE = 0x0800_0000
WS_EX_APPWINDOW = 0x0004_0000
WS_EX_TOOLWINDOW = 0x0000_0080
WS_EX_TRANSPARENT = 0x0000_0020
WS_EX_NOREDIRECTIONBITMAP = 0x0020_0000

MIN_WORKER_WINDOW_SIZE = 400

SWP_NOMOVE = 0x0002
SWP_NOSIZE = 0x0001
SWP_NOZORDER = 0x0004
SWP_NOACTIVATE = 0x0010
SWP_FRAMECHANGED = 0x0020
SWP_SHOWWINDOW = 0x0040

HWND_TOP = wintypes.HWND(0)
HWND_BOTTOM = wintypes.HWND(1)

SM_XVIRTUALSCREEN = 76
SM_YVIRTUALSCREEN = 77
SM_CXVIRTUALSCREEN = 78
SM_CYVIRTUALSCREEN = 79

WM_SPAWN_WORKER = 0x052C
SPAWN_WORKER_WPARAM = 0xD
SMTO_NORMAL = 0x0000

user32 = ctypes.windll.user32

EnumWindowsProc = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

_LONG_PTR = ctypes.c_longlong if ctypes.sizeof(ctypes.c_void_p) == 8 else ctypes.c_long

if ctypes.sizeof(ctypes.c_void_p) == 8:
    _get_window_long = user32.GetWindowLongPtrW
    _set_window_long = user32.SetWindowLongPtrW
else:
    _get_window_long = user32.GetWindowLongW
    _set_window_long = user32.SetWindowLongW

_get_window_long.restype = _LONG_PTR
_get_window_long.argtypes = [wintypes.HWND, ctypes.c_int]
_set_window_long.restype = _LONG_PTR
_set_window_long.argtypes = [wintypes.HWND, ctypes.c_int, _LONG_PTR]


def _window_style(value: int) -> int:
    """DWORD-стиль окна: маска 32 бита для SetWindowLong(Ptr)."""
    return value & 0xFFFF_FFFF


class _POINT(ctypes.Structure):
    _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long)]


@dataclass(frozen=True, slots=True)
class _RaisedDesktopLayout:
    """Win11 24H2+: Progman + дочерние SHELLDLL_DefView и WorkerW."""

    progman: int
    shell_view: int
    wallpaper_worker: int


class WindowsDesktopBackend(DesktopBackend):
    """Прикрепляет QWidget к WorkerW за иконками рабочего стола."""

    def __init__(self) -> None:
        self._worker_window: int = 0
        self._raised_layout: _RaisedDesktopLayout | None = None
        self._attached_handle: int = 0
        self._saved_style: int = 0
        self._saved_ex_style: int = 0
        self._target_bounds: QRect | None = None
        self._display_hooked = False

    def apply(self, window: QWidget, screen: QScreen) -> None:
        self._ensure_native_handle(window)
        self._target_bounds = screen.geometry()

        if self._attached_handle and self._attached_handle != int(window.winId()):
            self.remove(window)

        if not self._attached_handle:
            self._attach(int(window.winId()))

        self._fit_to_target_bounds(self._attached_handle)
        self._ensure_display_hook()

    def remove(self, window: QWidget) -> None:
        handle = int(window.winId())
        if self._attached_handle != handle:
            return

        self._detach()
        self._remove_display_hook()

    def set_screen(self, window: QWidget, screen: QScreen) -> None:
        self._target_bounds = screen.geometry()
        if not self._attached_handle:
            return

        # Только Win32 SetWindowPos: Qt resize()/setGeometry() после SetParent(WorkerW)
        # сбрасывает HWND в (0,0) клиентской области Progman — неверный монитор.
        self._fit_to_target_bounds(self._attached_handle)
        if self._raised_layout is not None:
            self._apply_raised_desktop_z_order(self._attached_handle)

    def dispose(self) -> None:
        if self._attached_handle:
            self._detach()
        self._worker_window = 0
        self._raised_layout = None
        self._remove_display_hook()

    def _ensure_native_handle(self, window: QWidget) -> None:
        if not window.testAttribute(Qt.WidgetAttribute.WA_NativeWindow):
            window.setAttribute(Qt.WidgetAttribute.WA_NativeWindow, True)
        window.winId()

    def _ensure_display_hook(self) -> None:
        if self._display_hooked:
            return

        app = QGuiApplication.instance()
        if app is not None:
            app.screenAdded.connect(self._on_display_changed)
            app.screenRemoved.connect(self._on_display_changed)
            app.primaryScreenChanged.connect(self._on_display_changed)
        self._display_hooked = True

    def _remove_display_hook(self) -> None:
        if not self._display_hooked:
            return

        app = QGuiApplication.instance()
        if app is not None:
            app.screenAdded.disconnect(self._on_display_changed)
            app.screenRemoved.disconnect(self._on_display_changed)
            app.primaryScreenChanged.disconnect(self._on_display_changed)
        self._display_hooked = False

    def _on_display_changed(self, *_args: object) -> None:
        if not self._attached_handle:
            return

        self._fit_to_target_bounds(self._attached_handle)
        if self._raised_layout is not None:
            self._apply_raised_desktop_z_order(self._attached_handle)

    def _initialize_worker(self) -> None:
        if self._worker_window:
            return

        if _is_raised_desktop():
            self._raised_layout = _ensure_raised_desktop_layout()
            self._worker_window = self._raised_layout.progman
            return

        self._raised_layout = None
        self._worker_window = _resolve_worker_window()

    def _apply_raised_desktop_z_order(self, window_handle: int) -> None:
        if self._raised_layout is None:
            return

        flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
        user32.SetWindowPos(
            window_handle,
            self._raised_layout.shell_view,
            0,
            0,
            0,
            0,
            flags,
        )
        user32.SetWindowPos(
            self._raised_layout.wallpaper_worker,
            window_handle,
            0,
            0,
            0,
            0,
            flags,
        )

    def _attach(self, window_handle: int) -> None:
        if not window_handle:
            raise ValueError("Некорректный HWND.")

        self._initialize_worker()

        self._saved_style = _window_style(_get_window_long(window_handle, GWL_STYLE))
        self._saved_ex_style = _window_style(_get_window_long(window_handle, GWL_EXSTYLE))

        style = _window_style(
            self._saved_style
            & ~(
                WS_CAPTION
                | WS_THICKFRAME
                | WS_MINIMIZEBOX
                | WS_MAXIMIZEBOX
                | WS_SYSMENU
                | WS_BORDER
                | WS_POPUP
            )
            | WS_CHILD
            | WS_VISIBLE
        )
        _set_window_long(window_handle, GWL_STYLE, style)

        ex_style = _window_style(
            (self._saved_ex_style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW
        )
        _set_window_long(window_handle, GWL_EXSTYLE, ex_style)

        user32.SetParent(window_handle, self._worker_window)
        self._fit_to_target_bounds(window_handle)
        if self._raised_layout is not None:
            self._apply_raised_desktop_z_order(window_handle)
        else:
            _send_to_bottom_of_parent(window_handle)
            _ensure_desktop_icons_above_wallpaper()
        _enable_mouse_click_through(window_handle)

        self._attached_handle = window_handle

    def _detach(self) -> None:
        if not self._attached_handle:
            return

        user32.SetParent(self._attached_handle, 0)
        _set_window_long(self._attached_handle, GWL_STYLE, self._saved_style)
        _set_window_long(self._attached_handle, GWL_EXSTYLE, self._saved_ex_style)
        self._attached_handle = 0

    def _fit_to_target_bounds(self, window_handle: int) -> None:
        bounds = self._target_bounds or _get_virtual_screen_bounds()
        self._fit_to_bounds(window_handle, bounds)

    def _fit_to_bounds(self, window_handle: int, screen_bounds: QRect) -> None:
        position = _POINT(screen_bounds.x(), screen_bounds.y())
        if self._worker_window:
            user32.ScreenToClient(self._worker_window, ctypes.byref(position))

        user32.SetWindowPos(
            window_handle,
            0,
            position.x,
            position.y,
            screen_bounds.width(),
            screen_bounds.height(),
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW,
        )


def _is_raised_desktop() -> bool:
    progman = user32.FindWindowW("Progman", None)
    if not progman:
        return False

    ex_style = _window_style(_get_window_long(progman, GWL_EXSTYLE))
    return bool(ex_style & WS_EX_NOREDIRECTIONBITMAP)


def _spawn_worker_layer(progman: int) -> None:
    result = ctypes.c_ulong()
    user32.SendMessageTimeoutW(
        progman,
        WM_SPAWN_WORKER,
        0,
        0,
        SMTO_NORMAL,
        1000,
        ctypes.byref(result),
    )


def _ensure_raised_desktop_layout() -> _RaisedDesktopLayout:
    progman = user32.FindWindowW("Progman", None)
    if not progman:
        raise RuntimeError("Окно Progman не найдено.")

    _spawn_worker_layer(progman)

    shell_view = user32.FindWindowExW(progman, 0, "SHELLDLL_DefView", None)
    if not shell_view:
        raise RuntimeError("SHELLDLL_DefView не найден в Progman.")

    wallpaper_worker = _find_progman_wallpaper_worker(progman)
    if not wallpaper_worker:
        raise RuntimeError("Дочерний WorkerW для обоев не найден в Progman.")

    return _RaisedDesktopLayout(
        progman=int(progman),
        shell_view=int(shell_view),
        wallpaper_worker=int(wallpaper_worker),
    )


def _find_progman_wallpaper_worker(progman: int) -> int:
    best_handle = 0
    best_area = 0
    child = 0

    while True:
        child = user32.FindWindowExW(progman, child, "WorkerW", None)
        if not child:
            break

        width, height = _window_size(child)
        if width < MIN_WORKER_WINDOW_SIZE or height < MIN_WORKER_WINDOW_SIZE:
            continue

        area = width * height
        if area > best_area:
            best_area = area
            best_handle = int(child)

    return best_handle


def _window_size(window_handle: int) -> tuple[int, int]:
    rect = wintypes.RECT()
    user32.GetWindowRect(window_handle, ctypes.byref(rect))
    return rect.right - rect.left, rect.bottom - rect.top


def _resolve_worker_window() -> int:
    progman = user32.FindWindowW("Progman", None)
    if not progman:
        raise RuntimeError("Окно Progman не найдено.")

    result = ctypes.c_ulong()
    user32.SendMessageTimeoutW(
        progman,
        WM_SPAWN_WORKER,
        SPAWN_WORKER_WPARAM,
        0,
        SMTO_NORMAL,
        1000,
        ctypes.byref(result),
    )

    worker = _try_find_empty_worker_window()
    if worker:
        return worker

    worker = _try_find_worker_w_sibling_of_shell_view_host()
    if worker:
        return worker

    raise RuntimeError(
        "Окно WorkerW для обоев не найдено. Перезапустите проводник или приложение."
    )


def _try_find_empty_worker_window() -> int:
    best_handle = 0
    best_area = 0

    @EnumWindowsProc
    def callback(top_level: int, _lparam: int) -> bool:
        nonlocal best_handle, best_area
        if not _has_window_class(top_level, "WorkerW"):
            return True
        if user32.FindWindowExW(top_level, 0, "SHELLDLL_DefView", None):
            return True

        width, height = _window_size(top_level)
        if width < MIN_WORKER_WINDOW_SIZE or height < MIN_WORKER_WINDOW_SIZE:
            return True

        area = width * height
        if area > best_area:
            best_area = area
            best_handle = top_level
        return True

    user32.EnumWindows(callback, 0)
    return best_handle


def _try_find_worker_w_sibling_of_shell_view_host() -> int:
    found = wintypes.HWND(0)

    @EnumWindowsProc
    def callback(top_level: int, _lparam: int) -> bool:
        nonlocal found
        shell_view = user32.FindWindowExW(top_level, 0, "SHELLDLL_DefView", None)
        if not shell_view:
            return True
        found = user32.FindWindowExW(0, top_level, "WorkerW", None)
        return not found

    user32.EnumWindows(callback, 0)
    return int(found)


def _has_window_class(window_handle: int, class_name: str) -> bool:
    buffer = ctypes.create_unicode_buffer(256)
    if user32.GetClassNameW(window_handle, buffer, 256) <= 0:
        return False
    return buffer.value == class_name


def _send_to_bottom_of_parent(window_handle: int) -> None:
    user32.SetWindowPos(
        window_handle,
        HWND_BOTTOM,
        0,
        0,
        0,
        0,
        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE,
    )


def _ensure_desktop_icons_above_wallpaper() -> None:
    progman = user32.FindWindowW("Progman", None)
    if not progman:
        return

    shell_view = user32.FindWindowExW(progman, 0, "SHELLDLL_DefView", None)
    if not shell_view:
        return

    flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
    user32.SetWindowPos(progman, HWND_TOP, 0, 0, 0, 0, flags)
    user32.SetWindowPos(shell_view, HWND_TOP, 0, 0, 0, 0, flags)


def _enable_mouse_click_through(window_handle: int) -> None:
    if not window_handle:
        return
    ex_style = _window_style(_get_window_long(window_handle, GWL_EXSTYLE))
    _set_window_long(window_handle, GWL_EXSTYLE, _window_style(ex_style | WS_EX_TRANSPARENT))


def _get_virtual_screen_bounds() -> QRect:
    return QRect(
        user32.GetSystemMetrics(SM_XVIRTUALSCREEN),
        user32.GetSystemMetrics(SM_YVIRTUALSCREEN),
        user32.GetSystemMetrics(SM_CXVIRTUALSCREEN),
        user32.GetSystemMetrics(SM_CYVIRTUALSCREEN),
    )

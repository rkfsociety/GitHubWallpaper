"""Linux: окно типа desktop (X11 _NET_WM_WINDOW_TYPE_DESKTOP; Wayland — fallback)."""

from __future__ import annotations

import ctypes
import logging
import os
import sys

from PySide6.QtCore import Qt
from PySide6.QtGui import QScreen
from PySide6.QtWidgets import QWidget

from github_wallpaper.desktop.backend import DesktopBackend

if not sys.platform.startswith("linux"):
    raise ImportError("linux_backend доступен только на Linux.")

_logger = logging.getLogger(__name__)

_NET_WM_WINDOW_TYPE = "_NET_WM_WINDOW_TYPE"
_NET_WM_WINDOW_TYPE_DESKTOP = "_NET_WM_WINDOW_TYPE_DESKTOP"


class LinuxDesktopBackend(DesktopBackend):
    """
    Размещает окно на слое рабочего стола.

    MVP: X11 через libX11. На Wayland — borderless + stays-on-bottom (без гарантий DE).
    """

    def __init__(self) -> None:
        self._attached_window: QWidget | None = None
        self._x11_available = _probe_x11()

    def apply(self, window: QWidget, screen: QScreen) -> None:
        self._prepare_window_flags(window, desktop_layer=self._x11_available)
        self._position_on_screen(window, screen)
        window.show()

        if self._x11_available:
            self._set_x11_desktop_type(window)
        else:
            _logger.warning(
                "X11 недоступен (Wayland?). Обои — borderless overlay; поведение зависит от DE."
            )

        self._attached_window = window

    def remove(self, window: QWidget) -> None:
        if self._attached_window is not window:
            return
        window.hide()
        self._attached_window = None

    def set_screen(self, window: QWidget, screen: QScreen) -> None:
        self._position_on_screen(window, screen)

    def dispose(self) -> None:
        if self._attached_window is not None:
            self._attached_window.hide()
            self._attached_window = None

    @staticmethod
    def _prepare_window_flags(window: QWidget, *, desktop_layer: bool) -> None:
        if desktop_layer:
            # X11 _NET_WM_WINDOW_TYPE_DESKTOP: без Tool — иначе перекрываем иконки рабочего стола.
            window.setWindowFlags(
                Qt.WindowType.FramelessWindowHint | Qt.WindowType.WindowDoesNotAcceptFocus
            )
            return

        window.setWindowFlags(
            Qt.WindowType.FramelessWindowHint
            | Qt.WindowType.WindowStaysOnBottomHint
            | Qt.WindowType.Tool
            | Qt.WindowType.WindowDoesNotAcceptFocus
        )

    @staticmethod
    def _position_on_screen(window: QWidget, screen: QScreen) -> None:
        window.setGeometry(screen.geometry())

    def _set_x11_desktop_type(self, window: QWidget) -> None:
        wid = int(window.winId())
        if not wid:
            return

        display = os.environ.get("DISPLAY")
        if not display:
            return

        try:
            x11 = ctypes.CDLL("libX11.so.6")
        except OSError:
            self._x11_available = False
            return

        x11.XOpenDisplay.restype = ctypes.c_void_p
        x11.XInternAtom.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_bool]
        x11.XInternAtom.restype = ctypes.c_ulong
        x11.XChangeProperty.argtypes = [
            ctypes.c_void_p,
            ctypes.c_ulong,
            ctypes.c_ulong,
            ctypes.c_ulong,
            ctypes.c_int,
            ctypes.c_int,
            ctypes.POINTER(ctypes.c_ulong),
            ctypes.c_int,
        ]
        x11.XLowerWindow.argtypes = [ctypes.c_void_p, ctypes.c_ulong]

        dpy = x11.XOpenDisplay(None)
        if not dpy:
            return

        try:
            wm_type_atom = x11.XInternAtom(dpy, _NET_WM_WINDOW_TYPE.encode(), False)
            desktop_atom = x11.XInternAtom(dpy, _NET_WM_WINDOW_TYPE_DESKTOP.encode(), False)
            xa_atom = ctypes.c_ulong(4)  # XA_ATOM

            atom_data = (ctypes.c_ulong * 1)(desktop_atom)
            x11.XChangeProperty(
                dpy,
                ctypes.c_ulong(wid),
                wm_type_atom,
                xa_atom,
                32,
                0,
                atom_data,
                1,
            )
            x11.XLowerWindow(dpy, ctypes.c_ulong(wid))
            x11.XFlush(dpy)
        finally:
            x11.XCloseDisplay(dpy)


def _probe_x11() -> bool:
    session = os.environ.get("XDG_SESSION_TYPE", "").lower()
    if session == "wayland":
        return False
    if not os.environ.get("DISPLAY"):
        return False
    try:
        x11 = ctypes.CDLL("libX11.so.6")
        x11.XOpenDisplay.restype = ctypes.c_void_p
        dpy = x11.XOpenDisplay(None)
        if dpy:
            x11.XCloseDisplay(dpy)
            return True
    except OSError:
        pass
    return False

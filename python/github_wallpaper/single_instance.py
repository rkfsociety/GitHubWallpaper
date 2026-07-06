"""Один экземпляр приложения: named mutex (Windows) · Unix socket (Linux)."""

from __future__ import annotations

import socket
import sys
from pathlib import Path

from github_wallpaper.paths import cache_dir

if sys.platform == "win32":
    import ctypes
    from ctypes import wintypes

    _MUTEX_NAME = r"Global\rkfsociety.GitHubWallpaper.SingleInstance"
    _ERROR_ALREADY_EXISTS = 183


class SingleInstanceGuard:
    """Удерживает блокировку до вызова release()."""

    def __init__(self) -> None:
        self._mutex_handle: int | None = None
        self._socket: socket.socket | None = None
        self._socket_path: Path | None = None

    def try_acquire(self) -> bool:
        if sys.platform == "win32":
            return self._try_acquire_windows()
        return self._try_acquire_unix()

    def release(self) -> None:
        if sys.platform == "win32":
            self._release_windows()
        else:
            self._release_unix()

    def _try_acquire_windows(self) -> bool:
        kernel32 = ctypes.windll.kernel32
        handle = kernel32.CreateMutexW(None, wintypes.BOOL(True), _MUTEX_NAME)
        if not handle:
            return False

        if kernel32.GetLastError() == _ERROR_ALREADY_EXISTS:
            kernel32.CloseHandle(handle)
            return False

        self._mutex_handle = handle
        return True

    def _release_windows(self) -> None:
        if self._mutex_handle is None:
            return

        kernel32 = ctypes.windll.kernel32
        kernel32.ReleaseMutex(self._mutex_handle)
        kernel32.CloseHandle(self._mutex_handle)
        self._mutex_handle = None

    def _try_acquire_unix(self) -> bool:
        socket_dir = cache_dir()
        socket_dir.mkdir(parents=True, exist_ok=True)
        self._socket_path = socket_dir / "instance.sock"

        if self._socket_path.exists():
            self._socket_path.unlink(missing_ok=True)

        sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        try:
            sock.bind(str(self._socket_path))
            sock.listen(1)
        except OSError:
            sock.close()
            return False

        self._socket = sock
        return True

    def _release_unix(self) -> None:
        if self._socket is not None:
            self._socket.close()
            self._socket = None

        if self._socket_path is not None:
            self._socket_path.unlink(missing_ok=True)
            self._socket_path = None

"""Точка входа bootstrap exe: установка runtime в AppData и запуск."""

from __future__ import annotations

import shutil
import subprocess
import sys
import threading
import tkinter as tk
from pathlib import Path
from tkinter import messagebox, ttk

from github_wallpaper.bootstrap import installer as bootstrap_installer
from github_wallpaper.bootstrap.runtime_paths import (
    is_runtime_installed,
    launcher_path,
    runtime_executable,
    runtime_root,
)
from github_wallpaper.paths import config_dir
from github_wallpaper.update.installer import format_download_error
from github_wallpaper.update.models import AppUpdateDownloadProgress


def main() -> int:
    try:
        config_dir().mkdir(parents=True, exist_ok=True)
        if is_runtime_installed():
            _ensure_launcher()
            return _launch_runtime(sys.argv[1:])

        return _install_and_launch()
    except Exception as ex:
        _show_fatal_error(format_download_error(ex) if _is_network_error(ex) else str(ex))
        return 1


def _install_and_launch() -> int:
    dialog = _InstallProgressDialog()
    error_holder: list[Exception] = []

    def worker() -> None:
        try:
            bootstrap_installer.install_runtime(dialog.report)
            _ensure_launcher()
            dialog.mark_ready()
        except Exception as ex:
            error_holder.append(ex)
            dialog.mark_failed()

    thread = threading.Thread(target=worker, daemon=True)
    thread.start()
    dialog.run()

    if error_holder:
        messagebox.showerror(
            "GitHub Wallpaper",
            f"Не удалось установить приложение:\n\n{format_download_error(error_holder[0])}",
            parent=dialog.root,
        )
        return 1

    return _launch_runtime(sys.argv[1:])


def _ensure_launcher() -> None:
    if not getattr(sys, "frozen", False):
        return

    source = Path(sys.executable).resolve()
    target = launcher_path()
    if source == target.resolve():
        return

    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)


def _launch_runtime(args: list[str]) -> int:
    executable = _resolve_runtime_executable()

    if sys.platform == "win32":
        _launch_windows_process(executable, args)
        return 0

    subprocess.Popen(
        [str(executable), *args],
        cwd=str(executable.parent),
        close_fds=False,
    )
    return 0


def _launch_windows_process(executable: Path, args: list[str]) -> None:
    """Запуск runtime через Win32 API — обход рекурсии one-file PyInstaller subprocess."""
    import ctypes
    from ctypes import wintypes

    CREATE_NEW_PROCESS_GROUP = 0x00000200
    DETACHED_PROCESS = 0x00000008

    class _STARTUPINFOW(ctypes.Structure):
        _fields_ = [
            ("cb", wintypes.DWORD),
            ("lpReserved", wintypes.LPWSTR),
            ("lpDesktop", wintypes.LPWSTR),
            ("lpTitle", wintypes.LPWSTR),
            ("dwX", wintypes.DWORD),
            ("dwY", wintypes.DWORD),
            ("dwXSize", wintypes.DWORD),
            ("dwYSize", wintypes.DWORD),
            ("dwXCountChars", wintypes.DWORD),
            ("dwYCountChars", wintypes.DWORD),
            ("dwFillAttribute", wintypes.DWORD),
            ("dwFlags", wintypes.DWORD),
            ("wShowWindow", wintypes.WORD),
            ("cbReserved2", wintypes.WORD),
            ("lpReserved2", ctypes.POINTER(wintypes.BYTE)),
            ("hStdInput", wintypes.HANDLE),
            ("hStdOutput", wintypes.HANDLE),
            ("hStdError", wintypes.HANDLE),
        ]

    class _PROCESS_INFORMATION(ctypes.Structure):
        _fields_ = [
            ("hProcess", wintypes.HANDLE),
            ("hThread", wintypes.HANDLE),
            ("dwProcessId", wintypes.DWORD),
            ("dwThreadId", wintypes.DWORD),
        ]

    command_line = subprocess.list2cmdline([str(executable), *args])
    startup_info = _STARTUPINFOW()
    startup_info.cb = ctypes.sizeof(_STARTUPINFOW)
    process_info = _PROCESS_INFORMATION()

    if not ctypes.windll.kernel32.CreateProcessW(
        str(executable),
        command_line,
        None,
        None,
        False,
        CREATE_NEW_PROCESS_GROUP | DETACHED_PROCESS,
        None,
        str(executable.parent),
        ctypes.byref(startup_info),
        ctypes.byref(process_info),
    ):
        error_code = ctypes.windll.kernel32.GetLastError()
        raise RuntimeError(f"Не удалось запустить runtime (код {error_code}).")

    ctypes.windll.kernel32.CloseHandle(process_info.hProcess)
    ctypes.windll.kernel32.CloseHandle(process_info.hThread)


def _resolve_runtime_executable() -> Path:
    name = "GitHubWallpaper.exe" if sys.platform == "win32" else "GitHubWallpaper"
    explicit = runtime_root() / name
    if explicit.is_file():
        executable = explicit.resolve()
    else:
        discovered = runtime_executable()
        if discovered is None:
            raise FileNotFoundError("Runtime не найден после установки.")
        executable = discovered.resolve()

    launcher = launcher_path().resolve()
    if executable == launcher:
        raise FileNotFoundError(
            "Runtime не найден: launcher и runtime указывают на один файл. "
            "Переустановите приложение или запустите GitHubWallpaper.exe из папки app."
        )
    return executable


def _is_network_error(exc: Exception) -> bool:
    name = exc.__class__.__name__
    return name in {"HTTPError", "ConnectError", "TimeoutException", "NetworkError"}


def _show_fatal_error(message: str) -> None:
    root = tk.Tk()
    root.withdraw()
    messagebox.showerror("GitHub Wallpaper", message, parent=root)
    root.destroy()


class _InstallProgressDialog:
    def __init__(self) -> None:
        self.root = tk.Tk()
        self.root.title("GitHub Wallpaper — установка")
        self.root.resizable(False, False)
        self.root.protocol("WM_DELETE_WINDOW", self._on_close)

        frame = ttk.Frame(self.root, padding=16)
        frame.grid(row=0, column=0, sticky="nsew")

        ttk.Label(
            frame,
            text="Скачивание компонентов приложения…",
            wraplength=360,
        ).grid(row=0, column=0, sticky="w")

        self._status = ttk.Label(frame, text="Подключение к GitHub…")
        self._status.grid(row=1, column=0, sticky="w", pady=(8, 8))

        self._progress = ttk.Progressbar(frame, mode="indeterminate", length=360)
        self._progress.grid(row=2, column=0, sticky="ew")
        self._progress.start(12)

        self._ready = False
        self._failed = False

    def report(self, progress: AppUpdateDownloadProgress) -> None:
        def update() -> None:
            if progress.percent is not None:
                self._progress.stop()
                self._progress.config(mode="determinate", maximum=100)
                self._progress["value"] = progress.percent
                total = progress.total_bytes or 0
                self._status.config(
                    text=f"Загружено {_format_size(progress.bytes_received)} из {_format_size(total)}",
                )
                return

            self._status.config(text=f"Загружено {_format_size(progress.bytes_received)}…")

        self.root.after(0, update)

    def mark_ready(self) -> None:
        self._ready = True
        self.root.after(0, self.root.quit)

    def mark_failed(self) -> None:
        self._failed = True
        self.root.after(0, self.root.quit)

    def run(self) -> None:
        self.root.mainloop()
        self.root.destroy()

    def _on_close(self) -> None:
        if self._ready or self._failed:
            self.root.quit()


def _format_size(value: int) -> str:
    if value >= 1_000_000:
        return f"{value / 1_000_000:.1f} МБ"
    if value >= 1_000:
        return f"{value / 1_000:.0f} КБ"
    return f"{value} Б"


if __name__ == "__main__":
    raise SystemExit(main())

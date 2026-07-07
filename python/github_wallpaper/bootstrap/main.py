"""Точка входа bootstrap exe: установка runtime в AppData и запуск."""

from __future__ import annotations

import io
import shutil
import subprocess
import sys
from pathlib import Path

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
    _configure_console_encoding()
    try:
        config_dir().mkdir(parents=True, exist_ok=True)
        if is_runtime_installed():
            _ensure_launcher()
            _ensure_runtime_up_to_date()
            return _launch_runtime(sys.argv[1:])

        return _install_and_launch()
    except Exception as ex:
        _show_fatal_error(format_download_error(ex) if _is_network_error(ex) else str(ex))
        return 1


def _ensure_runtime_up_to_date() -> None:
    try:
        if not bootstrap_installer.runtime_needs_update():
            return
    except Exception as ex:
        print(
            f"Не удалось проверить обновление runtime: {ex}. Запуск текущей версии…",
            flush=True,
        )
        return

    print("GitHub Wallpaper — обновление компонентов с GitHub…", flush=True)
    try:
        bootstrap_installer.install_runtime(_report_console_progress)
        print("Обновление завершено.", flush=True)
    except Exception as ex:
        print(
            f"Не удалось обновить runtime: {format_download_error(ex) if _is_network_error(ex) else ex}. "
            "Запуск текущей версии…",
            flush=True,
        )


def _install_and_launch() -> int:
    print("GitHub Wallpaper — первая установка", flush=True)
    print("Скачивание компонентов приложения с GitHub…", flush=True)

    try:
        bootstrap_installer.install_runtime(_report_console_progress)
        _ensure_launcher()
    except Exception as ex:
        _show_fatal_error(f"Не удалось установить приложение:\n\n{format_download_error(ex)}")
        return 1

    print("Установка завершена. Запуск…", flush=True)
    return _launch_runtime(sys.argv[1:])


def _report_console_progress(progress: AppUpdateDownloadProgress) -> None:
    if progress.percent is not None:
        total = progress.total_bytes or 0
        print(
            f"\rЗагружено {_format_size(progress.bytes_received)} из {_format_size(total)} "
            f"({progress.percent}%)",
            end="",
            flush=True,
        )
        if progress.percent >= 100:
            print(flush=True)
        return

    print(f"Загружено {_format_size(progress.bytes_received)}…", flush=True)


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
    if sys.platform == "win32":
        import ctypes

        ctypes.windll.user32.MessageBoxW(None, message, "GitHub Wallpaper", 0x10)
        return

    print(f"Ошибка: {message}", file=sys.stderr, flush=True)


def _format_size(value: int) -> str:
    if value >= 1_000_000:
        return f"{value / 1_000_000:.1f} МБ"
    if value >= 1_000:
        return f"{value / 1_000:.0f} КБ"
    return f"{value} Б"


def _configure_console_encoding() -> None:
    """PyInstaller one-file на Windows часто отдаёт stdout/stderr с ascii."""
    for stream_name in ("stdout", "stderr"):
        stream = getattr(sys, stream_name, None)
        if stream is None:
            continue
        encoding = getattr(stream, "encoding", None)
        if encoding and encoding.lower() not in {"ascii", "us-ascii"}:
            continue
        buffer = getattr(stream, "buffer", None)
        if buffer is None:
            continue
        if hasattr(stream, "detach"):
            stream.detach()
        setattr(
            sys,
            stream_name,
            io.TextIOWrapper(buffer, encoding="utf-8", errors="replace", line_buffering=True),
        )


if __name__ == "__main__":
    raise SystemExit(main())

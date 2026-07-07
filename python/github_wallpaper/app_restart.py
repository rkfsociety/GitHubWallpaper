"""Перезапуск приложения из трея (аналог AppInstaller.ScheduleRestart)."""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path


def schedule_restart() -> None:
    """
    Запускает новую копию после короткой задержки.

    Текущий процесс должен завершиться, чтобы освободить single-instance mutex.
    """
    executable, args, work_dir = _resolve_launch_target()

    if sys.platform == "win32":
        _schedule_restart_windows(executable, args, work_dir)
        return

    _schedule_restart_unix(executable, args, work_dir)


def _resolve_launch_target() -> tuple[Path, list[str], Path]:
    if getattr(sys, "frozen", False):
        executable = Path(sys.executable).resolve()
        return executable, [], executable.parent

    executable = Path(sys.executable).resolve()
    return executable, ["-m", "github_wallpaper.main"], Path.cwd()


def _restart_work_directory() -> Path:
    work_dir = Path(os.environ.get("TEMP", os.environ.get("TMP", "/tmp"))) / "GitHubWallpaper"
    work_dir.mkdir(parents=True, exist_ok=True)
    return work_dir


def _schedule_restart_windows(executable: Path, args: list[str], work_dir: Path) -> None:
    script_path = _restart_work_directory() / "restart.cmd"
    command = _format_windows_command(executable, args)
    script = f"""@echo off
setlocal
ping 127.0.0.1 -n 3 >nul
start "" /D "{_escape_cmd_path(work_dir)}" {command}
del "%~f0"
"""
    script_path.write_text(script, encoding="utf-8")

    subprocess.Popen(
        ["cmd.exe", "/c", str(script_path)],
        creationflags=subprocess.CREATE_NO_WINDOW,
        close_fds=True,
    )


def _schedule_restart_unix(executable: Path, args: list[str], work_dir: Path) -> None:
    script_path = _restart_work_directory() / "restart.sh"
    command = " ".join(_shell_quote(part) for part in [str(executable), *args])
    script = f"""#!/bin/sh
sleep 2
cd {_shell_quote(str(work_dir))} || exit 1
nohup {command} >/dev/null 2>&1 &
rm -f "$0"
"""
    script_path.write_text(script, encoding="utf-8")
    os.chmod(script_path, 0o755)

    subprocess.Popen(
        ["/bin/sh", str(script_path)],
        start_new_session=True,
        close_fds=True,
    )


def _format_windows_command(executable: Path, args: list[str]) -> str:
    parts = [_escape_cmd_path(executable), *(_escape_cmd_path(arg) for arg in args)]
    return " ".join(parts)


def _escape_cmd_path(value: Path | str) -> str:
    return f'"{Path(value)}"'


def _shell_quote(value: str) -> str:
    if not value:
        return "''"
    if all(char not in " '\"\\$`" for char in value):
        return value
    return "'" + value.replace("'", "'\"'\"'") + "'"

"""Авторизация через GitHub CLI (`gh`)."""

from __future__ import annotations

import re
import shutil
import subprocess
import sys
from dataclasses import dataclass

from github_wallpaper.github.oauth import defaults as oauth_defaults

DEFAULT_HOSTNAME = "github.com"
LOGIN_TIMEOUT_SECONDS = 300
DEFAULT_COMMAND_TIMEOUT_SECONDS = 60


class GhCliAuthError(Exception):
    """Ошибка взаимодействия с GitHub CLI."""


@dataclass(frozen=True, slots=True)
class GhCliAuthStatus:
    """Состояние `gh auth status` для github.com."""

    available: bool
    logged_in: bool
    username: str | None = None
    scopes: tuple[str, ...] = ()
    missing_scopes: tuple[str, ...] = ()
    message: str | None = None


def find_gh_executable() -> str | None:
    return shutil.which("gh")


def get_status(hostname: str = DEFAULT_HOSTNAME) -> GhCliAuthStatus:
    gh = find_gh_executable()
    if gh is None:
        return GhCliAuthStatus(
            available=False,
            logged_in=False,
            message="GitHub CLI (gh) не найден в PATH.",
        )

    returncode, stdout, stderr = _run_gh(
        "auth",
        "status",
        "-h",
        hostname,
        timeout=DEFAULT_COMMAND_TIMEOUT_SECONDS,
    )
    combined = _combine_output(stdout, stderr)
    logged_in = returncode == 0 and "logged in to" in combined.lower()
    if not logged_in:
        detail = _first_nonempty_line(combined) or "Выполните вход через GitHub CLI."
        return GhCliAuthStatus(
            available=True,
            logged_in=False,
            message=detail,
        )

    username_match = re.search(
        rf"logged in to {re.escape(hostname)} account (\S+)",
        combined,
        flags=re.IGNORECASE,
    )
    scopes = _parse_scopes(combined)
    missing_scopes = _parse_missing_scopes(combined)
    return GhCliAuthStatus(
        available=True,
        logged_in=True,
        username=username_match.group(1) if username_match else None,
        scopes=scopes,
        missing_scopes=missing_scopes,
    )


def read_token(hostname: str = DEFAULT_HOSTNAME) -> str | None:
    if find_gh_executable() is None:
        return None

    returncode, stdout, stderr = _run_gh(
        "auth",
        "token",
        "-h",
        hostname,
        timeout=DEFAULT_COMMAND_TIMEOUT_SECONDS,
    )
    if returncode != 0:
        return None

    token = stdout.strip()
    return token or None


def login_web(
    hostname: str = DEFAULT_HOSTNAME,
    *,
    scopes: tuple[str, ...] | None = None,
) -> None:
    """Браузерный вход через `gh auth login --web`."""
    if find_gh_executable() is None:
        raise GhCliAuthError("GitHub CLI (gh) не найден в PATH.")

    requested_scopes = scopes or _required_scopes()
    args = [
        "auth",
        "login",
        "--web",
        "--hostname",
        hostname,
        "--git-protocol",
        "https",
        "--skip-ssh-key",
    ]
    for scope in requested_scopes:
        args.extend(["-s", scope])

    returncode, stdout, stderr = _run_gh(*args, timeout=LOGIN_TIMEOUT_SECONDS)
    if returncode != 0:
        raise GhCliAuthError(_format_gh_error(returncode, stdout, stderr))

    status = get_status(hostname)
    if not status.logged_in:
        raise GhCliAuthError("Вход через gh не завершён.")


def refresh_scopes(
    hostname: str = DEFAULT_HOSTNAME,
    *,
    scopes: tuple[str, ...] | None = None,
) -> None:
    """Запрашивает недостающие scope через `gh auth refresh`."""
    if find_gh_executable() is None:
        raise GhCliAuthError("GitHub CLI (gh) не найден в PATH.")

    requested_scopes = scopes or _required_scopes()
    args = ["auth", "refresh", "-h", hostname]
    for scope in requested_scopes:
        args.extend(["-s", scope])

    returncode, stdout, stderr = _run_gh(*args, timeout=LOGIN_TIMEOUT_SECONDS)
    if returncode != 0:
        raise GhCliAuthError(_format_gh_error(returncode, stdout, stderr))


def import_token(hostname: str = DEFAULT_HOSTNAME) -> str:
    """Читает токен из gh и проверяет, что вход выполнен."""
    status = get_status(hostname)
    if not status.available:
        raise GhCliAuthError(status.message or "GitHub CLI (gh) недоступен.")
    if not status.logged_in:
        raise GhCliAuthError(status.message or "В GitHub CLI нет активного входа.")

    token = read_token(hostname)
    if not token:
        raise GhCliAuthError("Не удалось получить токен через `gh auth token`.")

    return token


def _required_scopes() -> tuple[str, ...]:
    return tuple(scope for scope in oauth_defaults.SCOPE.split() if scope)


def _run_gh(*args: str, timeout: float) -> tuple[int, str, str]:
    gh = find_gh_executable()
    if gh is None:
        raise GhCliAuthError("GitHub CLI (gh) не найден в PATH.")

    try:
        completed = subprocess.run(
            [gh, *args],
            capture_output=True,
            text=True,
            timeout=timeout,
            encoding="utf-8",
            errors="replace",
            **_login_subprocess_kwargs(),
        )
    except subprocess.TimeoutExpired as ex:
        raise GhCliAuthError("Команда gh превысила время ожидания.") from ex
    except OSError as ex:
        raise GhCliAuthError(f"Не удалось запустить gh: {ex}") from ex

    return completed.returncode, completed.stdout, completed.stderr


def _login_subprocess_kwargs() -> dict[str, object]:
    if sys.platform != "win32":
        return {}
    return {"creationflags": subprocess.CREATE_NO_WINDOW}


def _combine_output(stdout: str, stderr: str) -> str:
    return "\n".join(part for part in (stdout, stderr) if part).strip()


def _first_nonempty_line(text: str) -> str | None:
    for line in text.splitlines():
        stripped = line.strip()
        if stripped:
            return stripped
    return None


def _parse_scopes(text: str) -> tuple[str, ...]:
    match = re.search(r"token scopes:\s*(.+)$", text, flags=re.IGNORECASE | re.MULTILINE)
    if not match:
        return ()
    return tuple(scope.strip("'\"") for scope in re.findall(r"'([^']+)'", match.group(1)))


def _parse_missing_scopes(text: str) -> tuple[str, ...]:
    match = re.search(r"missing required token scopes:\s*(.+)$", text, flags=re.IGNORECASE | re.MULTILINE)
    if not match:
        return ()
    return tuple(scope.strip("'\"") for scope in re.findall(r"'([^']+)'", match.group(1)))


def _format_gh_error(returncode: int, stdout: str, stderr: str) -> str:
    detail = _first_nonempty_line(_combine_output(stdout, stderr))
    if detail:
        return detail
    return f"Команда gh завершилась с кодом {returncode}."

"""Проверка и установка обновлений v2.0 из GitHub Releases."""

from __future__ import annotations

from collections.abc import Callable
from datetime import datetime, timezone
from typing import Protocol

from github_wallpaper.settings_store import SettingsStore
from github_wallpaper.update import defaults
from github_wallpaper.update.checker import AppUpdateChecker
from github_wallpaper.update import installer
from github_wallpaper.update.models import (
    AppUpdateCheckResult,
    AppUpdateDownloadProgress,
    AppUpdateInfo,
)


class _TokenProvider(Protocol):
    def __call__(self) -> str | None: ...


class AppUpdateService:
    """Служба проверки и установки обновлений приложения."""

    def __init__(
        self,
        settings_store: SettingsStore,
        *,
        token_provider: _TokenProvider | None = None,
    ) -> None:
        self._settings_store = settings_store
        self._token_provider = token_provider
        initial_token = token_provider() if token_provider is not None else None
        self._checker = AppUpdateChecker(token=initial_token)

    def check_for_updates(self) -> AppUpdateCheckResult:
        if self._token_provider is not None:
            self._checker.set_token(self._token_provider())
        return self._checker.check()

    def should_check_automatically(self) -> bool:
        settings = self._settings_store.load()
        if not settings.auto_check_for_updates:
            return False

        last_check = settings.last_update_check_utc
        if last_check is None:
            return True

        now = datetime.now(timezone.utc)
        last_utc = last_check.astimezone(timezone.utc)
        return now - last_utc >= defaults.AUTOMATIC_CHECK_INTERVAL

    def record_automatic_check(self) -> None:
        try:
            settings = self._settings_store.load()
            settings.last_update_check_utc = datetime.now(timezone.utc)
            self._settings_store.save(settings)
        except OSError:
            pass

    def download_and_apply(
        self,
        update: AppUpdateInfo,
        progress: Callable[[AppUpdateDownloadProgress], None] | None = None,
    ) -> None:
        token = self._token_provider() if self._token_provider is not None else None
        archive_path = installer.download(update, progress, token=token)
        installer.schedule_restart(archive_path)

    def close(self) -> None:
        self._checker.close()

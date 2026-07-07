"""Модели проверки и установки обновлений."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class AppUpdateInfo:
    version: str
    download_url: str
    release_page_url: str
    asset_size_bytes: int | None
    asset_name: str
    asset_id: int | None = None


@dataclass(frozen=True, slots=True)
class AppUpdateDownloadProgress:
    bytes_received: int
    total_bytes: int | None

    @property
    def percent(self) -> int | None:
        if self.total_bytes is None or self.total_bytes <= 0:
            return None
        return max(0, min(100, int(self.bytes_received * 100 / self.total_bytes)))


@dataclass(frozen=True, slots=True)
class UpToDate:
    current_version: str


@dataclass(frozen=True, slots=True)
class UpdateAvailable:
    update: AppUpdateInfo
    current_version: str


@dataclass(frozen=True, slots=True)
class Skipped:
    reason: str


@dataclass(frozen=True, slots=True)
class Failed:
    message: str


AppUpdateCheckResult = UpToDate | UpdateAvailable | Skipped | Failed

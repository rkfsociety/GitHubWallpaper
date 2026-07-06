"""Координация паузы обоев: ручная, полноэкранная, батарея (WallpaperPauseCoordinator.cs)."""

from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from github_wallpaper.wallpaper.controller import WallpaperController


class WallpaperPauseCoordinator:
    """Согласует ручную паузу из трея и автопаузу."""

    def __init__(self, wallpaper_controller: WallpaperController) -> None:
        self._wallpaper = wallpaper_controller
        self._user_paused = False
        self._fullscreen_paused = False
        self._battery_paused = False
        self._suppress_auto_pause = False

    @property
    def is_user_paused(self) -> bool:
        return self._user_paused

    def toggle_user_pause(self) -> None:
        if self._user_paused:
            self._user_paused = False
        elif self._wallpaper.is_paused:
            self._suppress_auto_pause = True
        else:
            self._user_paused = True
        self._apply_effective_pause()

    def set_fullscreen_paused(self, paused: bool) -> None:
        if self._fullscreen_paused == paused:
            return
        self._fullscreen_paused = paused
        if paused:
            self._suppress_auto_pause = False
        self._apply_effective_pause()

    def set_battery_paused(self, paused: bool) -> None:
        if self._battery_paused == paused:
            return
        self._battery_paused = paused
        if paused:
            self._suppress_auto_pause = False
        self._apply_effective_pause()

    def reset_auto_pause(self) -> None:
        self._fullscreen_paused = False
        self._battery_paused = False
        self._apply_effective_pause()

    def _apply_effective_pause(self) -> None:
        auto_paused = not self._suppress_auto_pause and (
            self._fullscreen_paused or self._battery_paused
        )
        should_pause = self._user_paused or auto_paused

        if should_pause and not self._wallpaper.is_paused:
            self._wallpaper.pause()
        elif not should_pause and self._wallpaper.is_paused:
            self._wallpaper.resume()

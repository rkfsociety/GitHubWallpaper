"""Минимальное чтение settings.json (совместимость с C# AppSettings)."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from github_wallpaper.paths import settings_file


@dataclass
class AppSettings:
    """Подмножество полей AppSettings.cs для этапа 6.2."""

    display_device_name: str = ""

    @classmethod
    def load(cls, path: Path | None = None) -> AppSettings:
        file_path = path or settings_file()
        if not file_path.is_file():
            return cls()

        try:
            data = json.loads(file_path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            return cls()

        return cls(
            display_device_name=str(data.get("displayDeviceName", "") or ""),
        )

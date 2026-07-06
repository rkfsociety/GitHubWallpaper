"""Минимальное чтение settings.json (совместимость с C# AppSettings)."""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

from github_wallpaper.paths import settings_file


@dataclass
class CardDisplaySettings:
    """Какие секции показывать на карточке репозитория (CardDisplaySettings.cs)."""

    show_description: bool = True
    show_stats: bool = True
    show_ci: bool = True
    show_release: bool = True
    show_heatmap: bool = True
    show_feed: bool = True
    show_pull_requests: bool = True
    show_issues: bool = True
    show_commits: bool = True

    def to_bridge_payload(self) -> dict[str, bool]:
        return {
            "description": self.show_description,
            "stats": self.show_stats,
            "ci": self.show_ci,
            "release": self.show_release,
            "heatmap": self.show_heatmap,
            "feed": self.show_feed,
            "pullRequests": self.show_pull_requests,
            "issues": self.show_issues,
            "commits": self.show_commits,
        }

    @classmethod
    def from_dict(cls, data: object) -> CardDisplaySettings:
        if not isinstance(data, dict):
            return cls()

        return cls(
            show_description=bool(data.get("description", True)),
            show_stats=bool(data.get("stats", True)),
            show_ci=bool(data.get("ci", True)),
            show_release=bool(data.get("release", True)),
            show_heatmap=bool(data.get("heatmap", True)),
            show_feed=bool(data.get("feed", True)),
            show_pull_requests=bool(data.get("pullRequests", True)),
            show_issues=bool(data.get("issues", True)),
            show_commits=bool(data.get("commits", True)),
        )


@dataclass
class AppSettings:
    """Подмножество полей AppSettings.cs для этапов 6.2–6.3."""

    display_device_name: str = ""
    grid_columns: int = 3
    grid_rows: int = 2
    repository_slots: list[str] = field(default_factory=list)
    card_display: CardDisplaySettings = field(default_factory=CardDisplaySettings)

    @classmethod
    def load(cls, path: Path | None = None) -> AppSettings:
        file_path = path or settings_file()
        if not file_path.is_file():
            return cls()

        try:
            data = json.loads(file_path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            return cls()

        if not isinstance(data, dict):
            return cls()

        slots = data.get("repositorySlots", [])
        if not isinstance(slots, list):
            slots = []

        return cls(
            display_device_name=str(data.get("displayDeviceName", "") or ""),
            grid_columns=int(data.get("gridColumns", 3) or 3),
            grid_rows=int(data.get("gridRows", 2) or 2),
            repository_slots=[str(item or "") for item in slots],
            card_display=CardDisplaySettings.from_dict(data.get("cardDisplay")),
        )

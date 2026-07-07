"""Чтение и запись settings.json (совместимость с C# AppSettings / SettingsStore)."""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

from github_wallpaper.github.poll_intervals import PollIntervalPreset, preset_from_settings
from github_wallpaper.paths import settings_file
from github_wallpaper.repo_url_parser import RepoReference, try_parse

_DEFAULT_GRID_COLUMNS = 3
_DEFAULT_GRID_ROWS = 2
_MIN_GRID_SIZE = 1
_MAX_GRID_SIZE = 6


def _clamp_grid_size(value: int, fallback: int) -> int:
    if value < _MIN_GRID_SIZE or value > _MAX_GRID_SIZE:
        return fallback
    return value


def _slot_capacity(columns: int, rows: int) -> int:
    return _clamp_grid_size(columns, _DEFAULT_GRID_COLUMNS) * _clamp_grid_size(rows, _DEFAULT_GRID_ROWS)


def _normalize_slots(slots: list[str], capacity: int) -> list[str]:
    normalized: list[str] = []
    for index in range(capacity):
        slug = slots[index].strip() if index < len(slots) and slots[index] else ""
        normalized.append(slug if slug else "")
    return normalized


def _enumerate_repository_slugs(slots: list[str]) -> list[str]:
    result: list[str] = []
    for slug in slots:
        trimmed = slug.strip()
        if trimmed:
            result.append(trimmed)
    return result


def _build_slots_from_repositories(
    columns: int,
    rows: int,
    current_slots: list[str],
    repositories: list[str],
) -> list[str]:
    capacity = _slot_capacity(columns, rows)
    slots = _normalize_slots(current_slots, capacity)
    known = {slug.lower() for slug in slots if slug}

    queue: list[str] = []
    for slug in repositories:
        trimmed = slug.strip()
        if not trimmed:
            continue
        key = trimmed.lower()
        if key in known:
            continue
        known.add(key)
        queue.append(trimmed)

    for index in range(len(slots)):
        if slots[index] or not queue:
            continue
        slots[index] = queue.pop(0)

    return slots


def _parse_datetime(value: object) -> datetime | None:
    if value is None:
        return None
    if isinstance(value, datetime):
        return value
    if not isinstance(value, str) or not value.strip():
        return None
    text = value.strip()
    if text.endswith("Z"):
        text = f"{text[:-1]}+00:00"
    try:
        parsed = datetime.fromisoformat(text)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        return parsed.replace(tzinfo=timezone.utc)
    return parsed


def _preset_to_json(preset: PollIntervalPreset) -> int:
    mapping = {
        PollIntervalPreset.ECONOMY: 0,
        PollIntervalPreset.NORMAL: 1,
        PollIntervalPreset.FREQUENT: 2,
    }
    return mapping.get(preset, 1)


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

    def clone(self) -> CardDisplaySettings:
        return CardDisplaySettings(
            show_description=self.show_description,
            show_stats=self.show_stats,
            show_ci=self.show_ci,
            show_release=self.show_release,
            show_heatmap=self.show_heatmap,
            show_feed=self.show_feed,
            show_pull_requests=self.show_pull_requests,
            show_issues=self.show_issues,
            show_commits=self.show_commits,
        )

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

    def to_dict(self) -> dict[str, bool]:
        return {
            "showDescription": self.show_description,
            "showStats": self.show_stats,
            "showCi": self.show_ci,
            "showRelease": self.show_release,
            "showHeatmap": self.show_heatmap,
            "showFeed": self.show_feed,
            "showPullRequests": self.show_pull_requests,
            "showIssues": self.show_issues,
            "showCommits": self.show_commits,
        }

    @classmethod
    def from_dict(cls, data: object) -> CardDisplaySettings:
        if not isinstance(data, dict):
            return cls()

        def _flag(*keys: str, default: bool = True) -> bool:
            for key in keys:
                if key in data:
                    return bool(data[key])
            return default

        return cls(
            show_description=_flag("showDescription", "description"),
            show_stats=_flag("showStats", "stats"),
            show_ci=_flag("showCi", "ci"),
            show_release=_flag("showRelease", "release"),
            show_heatmap=_flag("showHeatmap", "heatmap"),
            show_feed=_flag("showFeed", "feed"),
            show_pull_requests=_flag("showPullRequests", "pullRequests"),
            show_issues=_flag("showIssues", "issues"),
            show_commits=_flag("showCommits", "commits"),
        )


@dataclass
class AppSettings:
    """Сериализуемые настройки приложения (без PAT), совместимы с v1.0."""

    repositories: list[str] = field(default_factory=list)
    grid_columns: int = _DEFAULT_GRID_COLUMNS
    grid_rows: int = _DEFAULT_GRID_ROWS
    repository_slots: list[str] = field(default_factory=list)
    poll_interval_preset: PollIntervalPreset = PollIntervalPreset.NORMAL
    auto_start: bool = False
    pause_on_fullscreen: bool = True
    pause_on_battery: bool = True
    display_device_name: str = ""
    github_oauth_client_id: str = ""
    auto_check_for_updates: bool = True
    last_update_check_utc: datetime | None = None
    card_display: CardDisplaySettings = field(default_factory=CardDisplaySettings)
    settings_window_left: int | None = None
    settings_window_top: int | None = None

    def to_dict(self) -> dict[str, object]:
        payload: dict[str, object] = {
            "repositories": list(self.repositories),
            "gridColumns": self.grid_columns,
            "gridRows": self.grid_rows,
            "repositorySlots": list(self.repository_slots),
            "pollIntervalPreset": _preset_to_json(self.poll_interval_preset),
            "autoStart": self.auto_start,
            "pauseOnFullscreen": self.pause_on_fullscreen,
            "pauseOnBattery": self.pause_on_battery,
            "displayDeviceName": self.display_device_name,
            "githubOAuthClientId": self.github_oauth_client_id,
            "autoCheckForUpdates": self.auto_check_for_updates,
            "cardDisplay": self.card_display.to_dict(),
        }
        if self.last_update_check_utc is not None:
            payload["lastUpdateCheckUtc"] = self.last_update_check_utc.astimezone(timezone.utc).isoformat()
        if self.settings_window_left is not None:
            payload["settingsWindowLeft"] = self.settings_window_left
        if self.settings_window_top is not None:
            payload["settingsWindowTop"] = self.settings_window_top
        return payload

    @classmethod
    def from_dict(cls, data: dict[str, object]) -> AppSettings:
        repositories = data.get("repositories", [])
        if not isinstance(repositories, list):
            repositories = []

        slots = data.get("repositorySlots", [])
        if not isinstance(slots, list):
            slots = []

        return cls(
            repositories=[str(item or "") for item in repositories],
            grid_columns=int(data.get("gridColumns", _DEFAULT_GRID_COLUMNS) or _DEFAULT_GRID_COLUMNS),
            grid_rows=int(data.get("gridRows", _DEFAULT_GRID_ROWS) or _DEFAULT_GRID_ROWS),
            repository_slots=[str(item or "") for item in slots],
            poll_interval_preset=preset_from_settings(data.get("pollIntervalPreset")),
            auto_start=bool(data.get("autoStart", False)),
            pause_on_fullscreen=bool(data.get("pauseOnFullscreen", True)),
            pause_on_battery=bool(data.get("pauseOnBattery", True)),
            display_device_name=str(data.get("displayDeviceName", "") or ""),
            github_oauth_client_id=str(data.get("githubOAuthClientId", "") or ""),
            auto_check_for_updates=bool(data.get("autoCheckForUpdates", True)),
            last_update_check_utc=_parse_datetime(data.get("lastUpdateCheckUtc")),
            card_display=CardDisplaySettings.from_dict(data.get("cardDisplay")),
            settings_window_left=_optional_int(data.get("settingsWindowLeft")),
            settings_window_top=_optional_int(data.get("settingsWindowTop")),
        )

    @classmethod
    def load(cls, path: Path | None = None) -> AppSettings:
        return SettingsStore(path).load()

    def load_repositories(self, default: RepoReference | None = None) -> list[RepoReference]:
        """Список репозиториев из ячеек сетки (SettingsStore.LoadRepositories)."""
        fallback = default or _default_repository()
        seen: set[str] = set()
        repositories: list[RepoReference] = []

        for slug in self.repository_slots:
            if not slug or not slug.strip():
                continue
            reference = try_parse(slug.strip())
            if reference is None:
                continue
            key = reference.slug.lower()
            if key in seen:
                continue
            seen.add(key)
            repositories.append(reference)

        if not repositories:
            repositories.append(fallback)
        return repositories


def _optional_int(value: object) -> int | None:
    if value is None:
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _default_repository() -> RepoReference:
    from github_wallpaper.github.repo_poller import DEFAULT_REPOSITORY

    return DEFAULT_REPOSITORY


def _create_default() -> AppSettings:
    default_repo = _default_repository()
    return AppSettings(
        repositories=[default_repo.slug],
        grid_columns=_DEFAULT_GRID_COLUMNS,
        grid_rows=_DEFAULT_GRID_ROWS,
        repository_slots=[default_repo.slug],
    )


def normalize_settings(settings: AppSettings) -> AppSettings:
    """Нормализация и миграция legacy-поля repositories → repositorySlots."""
    settings.grid_columns = _clamp_grid_size(settings.grid_columns, _DEFAULT_GRID_COLUMNS)
    settings.grid_rows = _clamp_grid_size(settings.grid_rows, _DEFAULT_GRID_ROWS)

    if not settings.repository_slots and settings.repositories:
        settings.repository_slots = list(settings.repositories)

    capacity = _slot_capacity(settings.grid_columns, settings.grid_rows)
    settings.repository_slots = _normalize_slots(settings.repository_slots, capacity)

    slugs = _enumerate_repository_slugs(settings.repository_slots)
    if not slugs:
        default_slug = _default_repository().slug
        slugs = [default_slug]
        settings.repository_slots = _normalize_slots([default_slug], capacity)

    settings.repositories = slugs
    settings.card_display = settings.card_display.clone()
    return settings


class SettingsStore:
    """Загрузка и сохранение settings.json (паритет с SettingsStore.cs)."""

    def __init__(self, path: Path | None = None) -> None:
        self._path = path or settings_file()

    @property
    def path(self) -> Path:
        return self._path

    def load(self) -> AppSettings:
        if not self._path.is_file():
            return _create_default()

        try:
            data = json.loads(self._path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            return _create_default()

        if not isinstance(data, dict):
            return _create_default()

        return normalize_settings(AppSettings.from_dict(data))

    def save(self, settings: AppSettings) -> None:
        normalized = normalize_settings(settings)
        self._path.parent.mkdir(parents=True, exist_ok=True)
        json_text = json.dumps(normalized.to_dict(), ensure_ascii=False, indent=2)
        self._path.write_text(f"{json_text}\n", encoding="utf-8")

    def load_repositories(self) -> list[RepoReference]:
        return self.load().load_repositories()

    def save_repositories(self, repositories: list[RepoReference]) -> None:
        settings = self.load()
        slugs = [repository.slug for repository in repositories]
        settings.repositories = slugs
        settings.repository_slots = _build_slots_from_repositories(
            settings.grid_columns,
            settings.grid_rows,
            settings.repository_slots,
            slugs,
        )
        settings.repositories = _enumerate_repository_slugs(settings.repository_slots)
        self.save(settings)

    def save_grid_layout(self, columns: int, rows: int, slots: list[str]) -> None:
        settings = self.load()
        settings.grid_columns = _clamp_grid_size(columns, _DEFAULT_GRID_COLUMNS)
        settings.grid_rows = _clamp_grid_size(rows, _DEFAULT_GRID_ROWS)
        capacity = _slot_capacity(settings.grid_columns, settings.grid_rows)
        settings.repository_slots = _normalize_slots(slots, capacity)
        settings.repositories = _enumerate_repository_slugs(settings.repository_slots)
        self.save(settings)


# AppSettings.load делегирует в SettingsStore (обратная совместимость).

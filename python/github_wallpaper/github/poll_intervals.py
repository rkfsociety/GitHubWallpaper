"""Интервалы опроса GitHub API по пресетам (PollIntervals.cs)."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import timedelta
from enum import Enum


class PollIntervalPreset(Enum):
    ECONOMY = "economy"
    NORMAL = "normal"
    FREQUENT = "frequent"


@dataclass(frozen=True, slots=True)
class ActivityPollIntervals:
    metadata: timedelta
    commits: timedelta
    pull_requests: timedelta
    issues: timedelta
    releases: timedelta
    ci_runs: timedelta
    heatmap: timedelta
    events: timedelta


def base_interval(preset: PollIntervalPreset) -> timedelta:
    if preset is PollIntervalPreset.ECONOMY:
        return timedelta(minutes=20)
    if preset is PollIntervalPreset.FREQUENT:
        return timedelta(minutes=1)
    return timedelta(minutes=10)


def for_activity_preset(preset: PollIntervalPreset) -> ActivityPollIntervals:
    base = base_interval(preset)
    if preset is PollIntervalPreset.ECONOMY:
        heatmap = timedelta(hours=2)
    elif preset is PollIntervalPreset.FREQUENT:
        heatmap = timedelta(minutes=30)
    else:
        heatmap = timedelta(hours=1)

    return ActivityPollIntervals(
        metadata=base,
        commits=base,
        pull_requests=base,
        issues=base,
        releases=base,
        ci_runs=base,
        heatmap=heatmap,
        events=base,
    )


def preset_from_settings(value: str | int | None) -> PollIntervalPreset:
    if isinstance(value, int):
        by_index = {
            0: PollIntervalPreset.ECONOMY,
            1: PollIntervalPreset.NORMAL,
            2: PollIntervalPreset.FREQUENT,
        }
        return by_index.get(value, PollIntervalPreset.NORMAL)

    if not value:
        return PollIntervalPreset.NORMAL

    normalized = str(value).strip().lower()
    for item in PollIntervalPreset:
        if item.value == normalized or item.name.lower() == normalized:
            return item
    return PollIntervalPreset.NORMAL

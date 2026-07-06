"""Снимки данных опроса и парсеры JSON (RepoPollModels.cs, ActivityModels.cs)."""

from __future__ import annotations

import json
from dataclasses import dataclass
from datetime import datetime
from enum import Enum
from typing import Any

from github_wallpaper.repo_url_parser import RepoReference


@dataclass(frozen=True, slots=True)
class RepoMetadataSnapshot:
    repository: RepoReference
    full_name: str
    description: str | None
    stargazers_count: int
    forks_count: int
    open_issues_count: int
    html_url: str
    fetched_at: datetime


@dataclass(frozen=True, slots=True)
class RepoCommitSnapshot:
    sha: str
    message: str
    author_name: str | None
    author_date: datetime | None
    html_url: str


@dataclass(frozen=True, slots=True)
class RepoPullSnapshot:
    number: int
    title: str
    user_login: str | None
    created_at: datetime | None
    html_url: str


@dataclass(frozen=True, slots=True)
class RepoIssueSnapshot:
    number: int
    title: str
    user_login: str | None
    created_at: datetime | None
    html_url: str


@dataclass(frozen=True, slots=True)
class RepoReleaseSnapshot:
    id: int
    tag_name: str
    name: str
    is_prerelease: bool
    published_at: datetime | None
    html_url: str


@dataclass(frozen=True, slots=True)
class RepoCiRunSnapshot:
    id: int
    name: str
    status: str
    conclusion: str | None
    updated_at: datetime | None
    html_url: str


@dataclass(frozen=True, slots=True)
class HeatmapWeekSnapshot:
    total: int
    days: list[int]


@dataclass(frozen=True, slots=True)
class RepoHeatmapSnapshot:
    weeks: list[HeatmapWeekSnapshot]
    fetched_at: datetime


@dataclass(frozen=True, slots=True)
class RepoEventSnapshot:
    id: str
    type: str
    actor_login: str | None
    created_at: datetime | None
    summary: str
    html_url: str


@dataclass(frozen=True, slots=True)
class ActivityFeedItem:
    id: str
    kind: str
    title: str
    subtitle: str | None
    timestamp: datetime
    html_url: str
    is_new: bool


class RepoPollKind(Enum):
    METADATA = "metadata"
    COMMITS = "commits"
    PULL_REQUESTS = "pullRequests"
    ISSUES = "issues"
    RELEASES = "releases"
    CI_RUN = "ciRun"
    HEATMAP = "heatmap"
    EVENTS = "events"
    ACTIVITY_FEED = "activityFeed"


class GitHubPollErrorCode(Enum):
    NOT_FOUND = "not_found"
    FORBIDDEN = "forbidden"
    UNAUTHORIZED = "unauthorized"
    RATE_LIMITED = "rate_limited"
    NETWORK = "network"
    SERVER_ERROR = "server_error"
    UNKNOWN = "unknown"


def parse_metadata(repository: RepoReference, json_text: str) -> RepoMetadataSnapshot:
    from datetime import timezone

    root = json.loads(json_text)
    return RepoMetadataSnapshot(
        repository=repository,
        full_name=_read_string(root, "full_name") or repository.slug,
        description=_read_optional_string(root, "description"),
        stargazers_count=_read_int(root, "stargazers_count"),
        forks_count=_read_int(root, "forks_count"),
        open_issues_count=_read_int(root, "open_issues_count"),
        html_url=_read_string(root, "html_url") or repository.html_url,
        fetched_at=datetime.now(timezone.utc),
    )


def parse_commits(json_text: str) -> list[RepoCommitSnapshot]:
    root = json.loads(json_text)
    if not isinstance(root, list):
        return []

    commits: list[RepoCommitSnapshot] = []
    for item in root:
        commit = item.get("commit", {})
        message = _read_string(commit, "message") or ""
        first_line = message.split("\n", 1)[0]

        author = commit.get("author", {})
        commits.append(
            RepoCommitSnapshot(
                sha=_read_string(item, "sha") or "",
                message=first_line,
                author_name=_read_optional_string(author, "name"),
                author_date=_read_optional_date(author, "date"),
                html_url=_read_string(item, "html_url") or "",
            )
        )
    return commits


def parse_pulls(json_text: str) -> list[RepoPullSnapshot]:
    root = json.loads(json_text)
    if not isinstance(root, list):
        return []

    return [
        RepoPullSnapshot(
            number=_read_int(item, "number"),
            title=_read_string(item, "title") or "",
            user_login=_read_nested_string(item, "user", "login"),
            created_at=_read_optional_date(item, "created_at"),
            html_url=_read_string(item, "html_url") or "",
        )
        for item in root
    ]


def parse_issues(json_text: str) -> list[RepoIssueSnapshot]:
    root = json.loads(json_text)
    if not isinstance(root, list):
        return []

    issues: list[RepoIssueSnapshot] = []
    for item in root:
        if "pull_request" in item:
            continue
        issues.append(
            RepoIssueSnapshot(
                number=_read_int(item, "number"),
                title=_read_string(item, "title") or "",
                user_login=_read_nested_string(item, "user", "login"),
                created_at=_read_optional_date(item, "created_at"),
                html_url=_read_string(item, "html_url") or "",
            )
        )
    return issues


def parse_releases(json_text: str) -> list[RepoReleaseSnapshot]:
    root = json.loads(json_text)
    if not isinstance(root, list):
        return []

    releases: list[RepoReleaseSnapshot] = []
    for item in root:
        releases.append(
            RepoReleaseSnapshot(
                id=_read_int(item, "id"),
                tag_name=_read_string(item, "tag_name") or "",
                name=_read_string(item, "name") or _read_string(item, "tag_name") or "",
                is_prerelease=_read_bool(item, "prerelease"),
                published_at=_read_optional_date(item, "published_at"),
                html_url=_read_string(item, "html_url") or "",
            )
        )
    return releases


def parse_latest_ci_run(json_text: str) -> RepoCiRunSnapshot | None:
    root = json.loads(json_text)
    runs = root.get("workflow_runs")
    if not isinstance(runs, list) or not runs:
        return None

    first = runs[0]
    return RepoCiRunSnapshot(
        id=_read_int(first, "id"),
        name=_read_string(first, "name") or "Workflow",
        status=_read_string(first, "status") or "unknown",
        conclusion=_read_optional_string(first, "conclusion"),
        updated_at=_read_optional_date(first, "updated_at"),
        html_url=_read_string(first, "html_url") or "",
    )


def parse_heatmap(json_text: str) -> RepoHeatmapSnapshot:
    from datetime import timezone

    root = json.loads(json_text)
    weeks: list[HeatmapWeekSnapshot] = []

    if isinstance(root, list):
        for week in root:
            days: list[int] = []
            raw_days = week.get("days")
            if isinstance(raw_days, list):
                for day in raw_days:
                    days.append(int(day) if isinstance(day, (int, float)) else 0)
            while len(days) < 7:
                days.append(0)
            weeks.append(HeatmapWeekSnapshot(total=_read_int(week, "total"), days=days))

    return RepoHeatmapSnapshot(weeks=weeks, fetched_at=datetime.now(timezone.utc))


def parse_events(json_text: str, repository: RepoReference) -> list[RepoEventSnapshot]:
    root = json.loads(json_text)
    if not isinstance(root, list):
        return []

    events: list[RepoEventSnapshot] = []
    for item in root:
        event_type = _read_string(item, "type") or "Event"
        actor = _read_nested_string(item, "actor", "login")
        created_at = _read_optional_date(item, "created_at")
        event_id = _read_string(item, "id") or f"{event_type}-{int(created_at.timestamp()) if created_at else 0}"
        events.append(
            RepoEventSnapshot(
                id=event_id,
                type=event_type,
                actor_login=actor,
                created_at=created_at,
                summary=_build_event_summary(event_type, actor, item),
                html_url=repository.html_url,
            )
        )
    return events


def _build_event_summary(event_type: str, actor: str | None, item: dict[str, Any]) -> str:
    login = actor or "someone"
    if event_type == "PushEvent":
        return f"{login} pushed commits"
    if event_type == "PullRequestEvent":
        action = _read_nested_string(item, "payload", "action") or "updated"
        return f"{login} {action} a pull request"
    if event_type == "IssuesEvent":
        action = _read_nested_string(item, "payload", "action") or "updated"
        return f"{login} {action} an issue"
    if event_type == "ReleaseEvent":
        return f"{login} published a release"
    if event_type == "CreateEvent":
        ref_type = _read_nested_string(item, "payload", "ref_type") or "resource"
        return f"{login} created {ref_type}"
    if event_type == "WatchEvent":
        return f"{login} starred the repository"
    if event_type == "ForkEvent":
        return f"{login} forked the repository"
    return f"{login}: {event_type}"


def _read_string(element: dict[str, Any], property_name: str) -> str | None:
    value = element.get(property_name)
    return value if isinstance(value, str) else None


def _read_optional_string(element: dict[str, Any], property_name: str) -> str | None:
    value = element.get(property_name)
    if value is None:
        return None
    return value if isinstance(value, str) else None


def _read_nested_string(element: dict[str, Any], object_name: str, property_name: str) -> str | None:
    nested = element.get(object_name)
    if not isinstance(nested, dict):
        return None
    return _read_string(nested, property_name)


def _read_int(element: dict[str, Any], property_name: str) -> int:
    value = element.get(property_name)
    if isinstance(value, bool):
        return int(value)
    if isinstance(value, int):
        return value
    if isinstance(value, float):
        return int(value)
    return 0


def _read_bool(element: dict[str, Any], property_name: str) -> bool:
    value = element.get(property_name)
    return bool(value) if isinstance(value, bool) else False


def _read_optional_date(element: dict[str, Any], property_name: str) -> datetime | None:
    text = _read_optional_string(element, property_name)
    if text is None:
        return None
    try:
        normalized = text.replace("Z", "+00:00")
        return datetime.fromisoformat(normalized)
    except ValueError:
        return None

"""Объединение событий API и дельт polling в ленту (ActivityAggregator.cs)."""

from __future__ import annotations

from dataclasses import replace
from datetime import datetime, timezone
from typing import Callable, TypeVar

from github_wallpaper.github.api_parsers import (
    ActivityFeedItem,
    RepoCiRunSnapshot,
    RepoCommitSnapshot,
    RepoEventSnapshot,
    RepoIssueSnapshot,
    RepoPullSnapshot,
    RepoReleaseSnapshot,
)
from github_wallpaper.repo_url_parser import RepoReference

_MAX_FEED_ITEMS = 30

TItem = TypeVar("TItem")
TKey = TypeVar("TKey")


class ActivityAggregator:
    """Единая лента активности по репозиторию."""

    def __init__(self) -> None:
        self._feeds: dict[str, _FeedState] = {}

    def integrate_events(
        self,
        repository: RepoReference,
        events: list[RepoEventSnapshot],
    ) -> list[ActivityFeedItem]:
        items = [
            ActivityFeedItem(
                id=f"event-{event.id}",
                kind=_map_event_kind(event.type),
                title=event.summary,
                subtitle=event.actor_login,
                timestamp=event.created_at or datetime.now(timezone.utc),
                html_url=event.html_url,
                is_new=False,
            )
            for event in events
        ]
        return self._merge_feed(repository, items)

    def integrate_commits(
        self,
        repository: RepoReference,
        commits: list[RepoCommitSnapshot],
    ) -> list[ActivityFeedItem]:
        state = self._get_state(repository)
        delta_items: list[ActivityFeedItem] = []
        is_bootstrap = len(state.seen_commit_shas) == 0

        for commit in commits:
            if not commit.sha or commit.sha in state.seen_commit_shas:
                continue
            state.seen_commit_shas.add(commit.sha)
            if is_bootstrap:
                continue
            delta_items.append(
                ActivityFeedItem(
                    id=f"commit-{commit.sha}",
                    kind="push",
                    title=commit.message,
                    subtitle=commit.author_name,
                    timestamp=commit.author_date or datetime.now(timezone.utc),
                    html_url=commit.html_url,
                    is_new=True,
                )
            )

        if not delta_items:
            return self.get_feed(repository)
        return self._merge_feed(repository, delta_items, prepend=True)

    def integrate_pulls(
        self,
        repository: RepoReference,
        pulls: list[RepoPullSnapshot],
    ) -> list[ActivityFeedItem]:
        return self._integrate_numbered_items(
            repository,
            pulls,
            lambda state: state.seen_pull_numbers,
            lambda pull: pull.number,
            lambda pull: ActivityFeedItem(
                id=f"pr-{pull.number}",
                kind="pr",
                title=pull.title,
                subtitle=pull.user_login,
                timestamp=pull.created_at or datetime.now(timezone.utc),
                html_url=pull.html_url,
                is_new=True,
            ),
        )

    def integrate_issues(
        self,
        repository: RepoReference,
        issues: list[RepoIssueSnapshot],
    ) -> list[ActivityFeedItem]:
        return self._integrate_numbered_items(
            repository,
            issues,
            lambda state: state.seen_issue_numbers,
            lambda issue: issue.number,
            lambda issue: ActivityFeedItem(
                id=f"issue-{issue.number}",
                kind="issue",
                title=issue.title,
                subtitle=issue.user_login,
                timestamp=issue.created_at or datetime.now(timezone.utc),
                html_url=issue.html_url,
                is_new=True,
            ),
        )

    def integrate_releases(
        self,
        repository: RepoReference,
        releases: list[RepoReleaseSnapshot],
    ) -> list[ActivityFeedItem]:
        return self._integrate_numbered_items(
            repository,
            releases,
            lambda state: state.seen_release_ids,
            lambda release: release.id,
            lambda release: ActivityFeedItem(
                id=f"release-{release.id}",
                kind="release",
                title=release.name,
                subtitle=release.tag_name,
                timestamp=release.published_at or datetime.now(timezone.utc),
                html_url=release.html_url,
                is_new=True,
            ),
        )

    def integrate_ci_run(
        self,
        repository: RepoReference,
        run: RepoCiRunSnapshot | None,
    ) -> list[ActivityFeedItem]:
        if run is None:
            return self.get_feed(repository)

        state = self._get_state(repository)
        signature = f"{run.id}:{run.status}:{run.conclusion}"
        if state.last_ci_signature == signature:
            return self.get_feed(repository)

        is_bootstrap = state.last_ci_signature is None
        state.last_ci_signature = signature
        if is_bootstrap:
            return self.get_feed(repository)

        if run.conclusion == "success":
            title = f"CI passed: {run.name}"
        elif run.conclusion == "failure":
            title = f"CI failed: {run.name}"
        elif run.conclusion == "cancelled":
            title = f"CI cancelled: {run.name}"
        else:
            title = f"CI {run.status}: {run.name}"

        updated_ts = int(run.updated_at.timestamp()) if run.updated_at else 0
        item = ActivityFeedItem(
            id=f"ci-{run.id}-{updated_ts}",
            kind="ci",
            title=title,
            subtitle=run.conclusion or run.status,
            timestamp=run.updated_at or datetime.now(timezone.utc),
            html_url=run.html_url,
            is_new=True,
        )
        return self._merge_feed(repository, [item], prepend=True)

    def get_feed(self, repository: RepoReference) -> list[ActivityFeedItem]:
        state = self._feeds.get(repository.slug.lower())
        if state is None:
            return []
        return list(state.items)

    def clear(self, repository: RepoReference) -> None:
        self._feeds.pop(repository.slug.lower(), None)

    def _integrate_numbered_items(
        self,
        repository: RepoReference,
        items: list[TItem],
        seen_selector: Callable[[_FeedState], set[TKey]],
        key_selector: Callable[[TItem], TKey],
        map_item: Callable[[TItem], ActivityFeedItem],
    ) -> list[ActivityFeedItem]:
        state = self._get_state(repository)
        seen = seen_selector(state)
        delta_items: list[ActivityFeedItem] = []
        is_bootstrap = len(seen) == 0

        for item in items:
            key = key_selector(item)
            if key in seen:
                continue
            seen.add(key)
            if is_bootstrap:
                continue
            delta_items.append(map_item(item))

        if not delta_items:
            return self.get_feed(repository)
        return self._merge_feed(repository, delta_items, prepend=True)

    def _merge_feed(
        self,
        repository: RepoReference,
        incoming: list[ActivityFeedItem],
        *,
        prepend: bool = False,
    ) -> list[ActivityFeedItem]:
        state = self._get_state(repository)
        merged: dict[str, ActivityFeedItem] = {}

        for existing in state.items:
            merged[existing.id] = replace(existing, is_new=False)

        for item in incoming:
            if item.id in merged:
                previous = merged[item.id]
                merged[item.id] = replace(item, is_new=item.is_new or previous.is_new)
            else:
                merged[item.id] = item

        ordered = sorted(merged.values(), key=lambda entry: entry.timestamp, reverse=True)[:_MAX_FEED_ITEMS]

        if prepend:
            newest_ids = {item.id for item in incoming}
            ordered = [
                replace(item, is_new=item.is_new or item.id in newest_ids) for item in ordered
            ]

        state.items = ordered
        return list(state.items)

    def _get_state(self, repository: RepoReference) -> _FeedState:
        key = repository.slug.lower()
        state = self._feeds.get(key)
        if state is None:
            state = _FeedState()
            self._feeds[key] = state
        return state


class _FeedState:
    def __init__(self) -> None:
        self.items: list[ActivityFeedItem] = []
        self.seen_commit_shas: set[str] = set()
        self.seen_pull_numbers: set[int] = set()
        self.seen_issue_numbers: set[int] = set()
        self.seen_release_ids: set[int] = set()
        self.last_ci_signature: str | None = None


def _map_event_kind(event_type: str) -> str:
    mapping = {
        "PushEvent": "push",
        "PullRequestEvent": "pr",
        "IssuesEvent": "issue",
        "ReleaseEvent": "release",
        "WatchEvent": "watch",
        "ForkEvent": "fork",
    }
    return mapping.get(event_type, "event")

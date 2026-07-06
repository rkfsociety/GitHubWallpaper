"""Периодический опрос GitHub-репозиториев (RepoPoller.cs)."""

from __future__ import annotations

import asyncio
import logging
import threading
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from enum import Enum
from typing import Any

from PySide6.QtCore import QObject, Signal

from github_wallpaper.github.activity_aggregator import ActivityAggregator
from github_wallpaper.github.api_parsers import (
    ActivityFeedItem,
    GitHubPollErrorCode,
    RepoCiRunSnapshot,
    RepoCommitSnapshot,
    RepoHeatmapSnapshot,
    RepoIssueSnapshot,
    RepoMetadataSnapshot,
    RepoPollKind,
    RepoPullSnapshot,
    RepoReleaseSnapshot,
    parse_commits,
    parse_events,
    parse_heatmap,
    parse_issues,
    parse_latest_ci_run,
    parse_metadata,
    parse_pulls,
    parse_releases,
)
from github_wallpaper.github.github_client import GitHubClient
from github_wallpaper.github.github_poll_error import GitHubPollError
from github_wallpaper.github.poll_intervals import (
    ActivityPollIntervals,
    PollIntervalPreset,
    for_activity_preset,
)
from github_wallpaper.repo_url_parser import RepoReference
from github_wallpaper.settings_store import CardDisplaySettings

_logger = logging.getLogger(__name__)

_LOOP_TICK = timedelta(seconds=30)
_PENDING_HEATMAP_POLL_INTERVAL = timedelta(minutes=2)
DEFAULT_REPOSITORY = RepoReference(owner="microsoft", repo="vscode")


class _EndpointKind(Enum):
    METADATA = "metadata"
    COMMITS = "commits"
    PULLS = "pulls"
    ISSUES = "issues"
    RELEASES = "releases"
    CI_RUN = "ci_run"
    HEATMAP = "heatmap"
    EVENTS = "events"


@dataclass
class _RepoPollState:
    last_metadata_poll: datetime | None = None
    last_commits_poll: datetime | None = None
    last_pulls_poll: datetime | None = None
    last_issues_poll: datetime | None = None
    last_releases_poll: datetime | None = None
    last_ci_poll: datetime | None = None
    last_heatmap_poll: datetime | None = None
    heatmap_pending: bool = False
    last_events_poll: datetime | None = None
    metadata: RepoMetadataSnapshot | None = None
    commits: list[RepoCommitSnapshot] | None = None
    pulls: list[RepoPullSnapshot] | None = None
    issues: list[RepoIssueSnapshot] | None = None
    releases: list[RepoReleaseSnapshot] | None = None
    ci_run: RepoCiRunSnapshot | None = None
    heatmap: RepoHeatmapSnapshot | None = None
    activity_feed: list[ActivityFeedItem] | None = None
    fatal_error: GitHubPollErrorCode | None = None
    etags: dict[_EndpointKind, str] = field(default_factory=dict)

    def get_etag(self, kind: _EndpointKind) -> str | None:
        return self.etags.get(kind)

    def set_etag(self, kind: _EndpointKind, etag: str | None) -> None:
        if etag and etag.strip():
            self.etags[kind] = etag


class RepoPoller(QObject):
    """Фоновый опрос GitHub API; события через Qt signals (thread-safe)."""

    metadata_updated = Signal(object, object)
    commits_updated = Signal(object, object)
    pulls_updated = Signal(object, object)
    issues_updated = Signal(object, object)
    releases_updated = Signal(object, object)
    ci_run_updated = Signal(object, object)
    heatmap_updated = Signal(object, object)
    activity_feed_updated = Signal(object, object)
    repositories_changed = Signal()
    poll_failed = Signal(object, object, object)

    def __init__(self, client: GitHubClient) -> None:
        super().__init__()
        self._client = client
        self._activity_aggregator = ActivityAggregator()
        self._lock = threading.RLock()
        self._states: dict[str, _RepoPollState] = {}
        self._repositories: list[RepoReference] = [DEFAULT_REPOSITORY]
        self._intervals = for_activity_preset(PollIntervalPreset.NORMAL)
        self._card_display = CardDisplaySettings()
        self._paused = False
        self._thread: threading.Thread | None = None
        self._stop_event = threading.Event()
        self._loop: asyncio.AbstractEventLoop | None = None

    @property
    def is_paused(self) -> bool:
        with self._lock:
            return self._paused

    @property
    def repositories(self) -> list[RepoReference]:
        with self._lock:
            return list(self._repositories)

    def configure_poll_intervals(self, preset: PollIntervalPreset) -> None:
        with self._lock:
            self._intervals = for_activity_preset(preset)

    def configure_card_display(self, settings: CardDisplaySettings) -> None:
        with self._lock:
            self._card_display = CardDisplaySettings(
                show_description=settings.show_description,
                show_stats=settings.show_stats,
                show_ci=settings.show_ci,
                show_release=settings.show_release,
                show_heatmap=settings.show_heatmap,
                show_feed=settings.show_feed,
                show_pull_requests=settings.show_pull_requests,
                show_issues=settings.show_issues,
                show_commits=settings.show_commits,
            )

    def start(self, repositories: list[RepoReference] | None = None) -> None:
        repos = list(repositories or [DEFAULT_REPOSITORY])
        if not repos:
            repos = [DEFAULT_REPOSITORY]

        with self._lock:
            self._repositories = repos

        self.stop()
        self._stop_event.clear()
        self.repositories_changed.emit()
        self._thread = threading.Thread(target=self._thread_main, name="RepoPoller", daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop_event.set()
        loop = self._loop
        if loop is not None and loop.is_running():
            loop.call_soon_threadsafe(lambda: None)

        if self._thread is not None and self._thread.is_alive():
            self._thread.join(timeout=5)
        self._thread = None
        self._loop = None

    def set_paused(self, paused: bool) -> None:
        with self._lock:
            self._paused = paused

    def get_cached_metadata(self, repository: RepoReference) -> RepoMetadataSnapshot | None:
        return self._get_state(repository).metadata

    def get_cached_commits(self, repository: RepoReference) -> list[RepoCommitSnapshot] | None:
        return self._get_state(repository).commits

    def get_cached_pulls(self, repository: RepoReference) -> list[RepoPullSnapshot] | None:
        return self._get_state(repository).pulls

    def get_cached_issues(self, repository: RepoReference) -> list[RepoIssueSnapshot] | None:
        return self._get_state(repository).issues

    def get_cached_releases(self, repository: RepoReference) -> list[RepoReleaseSnapshot] | None:
        return self._get_state(repository).releases

    def get_cached_ci_run(self, repository: RepoReference) -> RepoCiRunSnapshot | None:
        return self._get_state(repository).ci_run

    def get_cached_heatmap(self, repository: RepoReference) -> RepoHeatmapSnapshot | None:
        return self._get_state(repository).heatmap

    def get_cached_activity_feed(self, repository: RepoReference) -> list[ActivityFeedItem] | None:
        return self._get_state(repository).activity_feed

    def dispose(self) -> None:
        self.stop()

    def _thread_main(self) -> None:
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        self._loop = loop
        try:
            loop.run_until_complete(self._run_loop())
        except Exception:
            _logger.exception("RepoPoller: неожиданная ошибка цикла")
        finally:
            loop.run_until_complete(self._client.aclose())
            loop.close()

    async def _run_loop(self) -> None:
        await self._poll_all_due(include_never_polled=True)

        while not self._stop_event.is_set():
            try:
                await asyncio.sleep(_LOOP_TICK.total_seconds())
            except asyncio.CancelledError:
                break

            if self._stop_event.is_set():
                break

            await self._poll_all_due(include_never_polled=False)

    async def _poll_all_due(self, *, include_never_polled: bool) -> None:
        if self.is_paused:
            return

        with self._lock:
            repositories = list(self._repositories)
            intervals = self._intervals
            display = self._card_display

        if include_never_polled:
            await asyncio.gather(
                *(
                    self._poll_repository(repository, intervals, display, include_never_polled)
                    for repository in repositories
                )
            )
            return

        for repository in repositories:
            if self._stop_event.is_set():
                return
            await self._poll_repository(repository, intervals, display, include_never_polled)

    async def _poll_repository(
        self,
        repository: RepoReference,
        intervals: ActivityPollIntervals,
        display: CardDisplaySettings,
        include_never_polled: bool,
    ) -> None:
        state = self._get_state(repository)

        if _is_due(state.last_metadata_poll, intervals.metadata, include_never_polled):
            await self._poll_metadata(repository)

        state = self._get_state(repository)
        if state.fatal_error is not None:
            return

        if display.show_commits and _is_due(state.last_commits_poll, intervals.commits, include_never_polled):
            await self._poll_commits(repository)

        if (display.show_pull_requests or display.show_stats) and _is_due(
            state.last_pulls_poll, intervals.pull_requests, include_never_polled
        ):
            await self._poll_pulls(repository)

        if display.show_issues and _is_due(state.last_issues_poll, intervals.issues, include_never_polled):
            await self._poll_issues(repository)

        if display.show_release and _is_due(state.last_releases_poll, intervals.releases, include_never_polled):
            await self._poll_releases(repository)

        if display.show_ci and _is_due(state.last_ci_poll, intervals.ci_runs, include_never_polled):
            await self._poll_ci_run(repository)

        state = self._get_state(repository)
        heatmap_interval = (
            _PENDING_HEATMAP_POLL_INTERVAL if state.heatmap_pending else intervals.heatmap
        )
        if display.show_heatmap and _is_due(state.last_heatmap_poll, heatmap_interval, include_never_polled):
            await self._poll_heatmap(repository)

        if display.show_feed and _is_due(state.last_events_poll, intervals.events, include_never_polled):
            await self._poll_events(repository)

    async def _poll_metadata(self, repository: RepoReference) -> None:
        state = self._get_state(repository)
        path = f"/repos/{repository.owner}/{repository.repo}"

        try:
            result = await self._get_cached_async(state, _EndpointKind.METADATA, path)
            if result.is_not_modified:
                state.last_metadata_poll = datetime.now(timezone.utc)
                return

            snapshot = parse_metadata(repository, result.body)
            with self._lock:
                state.metadata = snapshot
                state.last_metadata_poll = datetime.now(timezone.utc)
                state.fatal_error = None

            self.metadata_updated.emit(repository, snapshot)
        except Exception as ex:
            error = GitHubPollError.from_exception(ex)
            if error.is_fatal_for_repo:
                with self._lock:
                    state.fatal_error = error.code
            self.poll_failed.emit(repository, RepoPollKind.METADATA, ex)

    async def _poll_commits(self, repository: RepoReference) -> None:
        state = self._get_state(repository)
        path = f"/repos/{repository.owner}/{repository.repo}/commits?per_page=5"

        try:
            result = await self._get_cached_async(state, _EndpointKind.COMMITS, path)
            if result.is_not_modified:
                state.last_commits_poll = datetime.now(timezone.utc)
                return

            commits = parse_commits(result.body)
            with self._lock:
                state.commits = commits
                state.last_commits_poll = datetime.now(timezone.utc)

            self.commits_updated.emit(repository, commits)
            feed = self._activity_aggregator.integrate_commits(repository, commits)
            self._publish_activity_feed(repository, state, feed)
        except Exception as ex:
            self.poll_failed.emit(repository, RepoPollKind.COMMITS, ex)

    async def _poll_pulls(self, repository: RepoReference) -> None:
        state = self._get_state(repository)
        path = f"/repos/{repository.owner}/{repository.repo}/pulls?state=open&per_page=10"

        try:
            result = await self._get_cached_async(state, _EndpointKind.PULLS, path)
            if result.is_not_modified:
                state.last_pulls_poll = datetime.now(timezone.utc)
                return

            pulls = parse_pulls(result.body)
            with self._lock:
                state.pulls = pulls
                state.last_pulls_poll = datetime.now(timezone.utc)

            self.pulls_updated.emit(repository, pulls)
            feed = self._activity_aggregator.integrate_pulls(repository, pulls)
            self._publish_activity_feed(repository, state, feed)
        except Exception as ex:
            self.poll_failed.emit(repository, RepoPollKind.PULL_REQUESTS, ex)

    async def _poll_issues(self, repository: RepoReference) -> None:
        state = self._get_state(repository)
        path = f"/repos/{repository.owner}/{repository.repo}/issues?state=open&per_page=10"

        try:
            result = await self._get_cached_async(state, _EndpointKind.ISSUES, path)
            if result.is_not_modified:
                state.last_issues_poll = datetime.now(timezone.utc)
                return

            issues = parse_issues(result.body)
            with self._lock:
                state.issues = issues
                state.last_issues_poll = datetime.now(timezone.utc)

            self.issues_updated.emit(repository, issues)
            feed = self._activity_aggregator.integrate_issues(repository, issues)
            self._publish_activity_feed(repository, state, feed)
        except Exception as ex:
            self.poll_failed.emit(repository, RepoPollKind.ISSUES, ex)

    async def _poll_releases(self, repository: RepoReference) -> None:
        state = self._get_state(repository)
        path = f"/repos/{repository.owner}/{repository.repo}/releases?per_page=5"

        try:
            result = await self._get_cached_async(state, _EndpointKind.RELEASES, path)
            if result.is_not_modified:
                state.last_releases_poll = datetime.now(timezone.utc)
                return

            releases = parse_releases(result.body)
            with self._lock:
                state.releases = releases
                state.last_releases_poll = datetime.now(timezone.utc)

            self.releases_updated.emit(repository, releases)
            feed = self._activity_aggregator.integrate_releases(repository, releases)
            self._publish_activity_feed(repository, state, feed)
        except Exception as ex:
            self.poll_failed.emit(repository, RepoPollKind.RELEASES, ex)

    async def _poll_ci_run(self, repository: RepoReference) -> None:
        state = self._get_state(repository)
        path = f"/repos/{repository.owner}/{repository.repo}/actions/runs?per_page=1"

        try:
            result = await self._get_cached_async(state, _EndpointKind.CI_RUN, path)
            if result.is_not_modified:
                state.last_ci_poll = datetime.now(timezone.utc)
                return

            run = parse_latest_ci_run(result.body)
            with self._lock:
                state.ci_run = run
                state.last_ci_poll = datetime.now(timezone.utc)

            self.ci_run_updated.emit(repository, run)
            feed = self._activity_aggregator.integrate_ci_run(repository, run)
            self._publish_activity_feed(repository, state, feed)
        except Exception as ex:
            self.poll_failed.emit(repository, RepoPollKind.CI_RUN, ex)

    async def _poll_heatmap(self, repository: RepoReference) -> None:
        state = self._get_state(repository)
        path = f"/repos/{repository.owner}/{repository.repo}/stats/commit_activity"

        try:
            etag = state.get_etag(_EndpointKind.HEATMAP)
            result = await self._client.get_stats(path, if_none_match=etag)

            if result is None:
                with self._lock:
                    state.heatmap_pending = True
                    state.last_heatmap_poll = datetime.now(timezone.utc)
                return

            if result.is_not_modified:
                state.last_heatmap_poll = datetime.now(timezone.utc)
                return

            state.set_etag(_EndpointKind.HEATMAP, result.etag)
            heatmap = parse_heatmap(result.body)
            with self._lock:
                state.heatmap = heatmap
                state.heatmap_pending = False
                state.last_heatmap_poll = datetime.now(timezone.utc)

            self.heatmap_updated.emit(repository, heatmap)
        except Exception as ex:
            self.poll_failed.emit(repository, RepoPollKind.HEATMAP, ex)

    async def _poll_events(self, repository: RepoReference) -> None:
        state = self._get_state(repository)
        path = f"/repos/{repository.owner}/{repository.repo}/events?per_page=30"

        try:
            result = await self._get_cached_async(state, _EndpointKind.EVENTS, path)
            if result.is_not_modified:
                state.last_events_poll = datetime.now(timezone.utc)
                return

            events = parse_events(result.body, repository)
            state.last_events_poll = datetime.now(timezone.utc)
            feed = self._activity_aggregator.integrate_events(repository, events)
            self._publish_activity_feed(repository, state, feed)
        except Exception as ex:
            self.poll_failed.emit(repository, RepoPollKind.EVENTS, ex)

    def _publish_activity_feed(
        self,
        repository: RepoReference,
        state: _RepoPollState,
        feed: list[ActivityFeedItem],
    ) -> None:
        with self._lock:
            state.activity_feed = feed
        self.activity_feed_updated.emit(repository, feed)

    async def _get_cached_async(self, state: _RepoPollState, kind: _EndpointKind, path: str):
        etag = state.get_etag(kind)
        result = await self._client.get(path, if_none_match=etag)
        if not result.is_not_modified and result.etag is not None:
            state.set_etag(kind, result.etag)
        return result

    def _get_state(self, repository: RepoReference) -> _RepoPollState:
        key = repository.slug.lower()
        with self._lock:
            state = self._states.get(key)
            if state is None:
                state = _RepoPollState()
                self._states[key] = state
            return state


def _is_due(last_poll: datetime | None, interval: timedelta, include_never_polled: bool) -> bool:
    if include_never_polled or last_poll is None:
        return True
    return datetime.now(timezone.utc) - last_poll >= interval

"""JSON-мост C# ↔ JS: сериализация camelCase, очередь до page:ready, open-url."""

from __future__ import annotations

import json
import logging
from collections import deque
from collections.abc import Callable
from datetime import datetime
from pathlib import Path
from typing import TYPE_CHECKING, Any
from urllib.parse import urlparse

from PySide6.QtCore import QObject, Slot
from PySide6.QtGui import QDesktopServices
from PySide6.QtCore import QUrl
from PySide6.QtWebChannel import QWebChannel
from PySide6.QtWebEngineCore import QWebEnginePage, QWebEngineScript

from github_wallpaper.github.api_parsers import (
    ActivityFeedItem,
    RepoCiRunSnapshot,
    RepoCommitSnapshot,
    RepoHeatmapSnapshot,
    RepoIssueSnapshot,
    RepoMetadataSnapshot,
    RepoPollKind,
    RepoPullSnapshot,
    RepoReleaseSnapshot,
)
from github_wallpaper.github.github_poll_error import GitHubPollError
from github_wallpaper.repo_url_parser import RepoReference, try_parse
from github_wallpaper.settings_store import AppSettings, CardDisplaySettings

if TYPE_CHECKING:
    from github_wallpaper.github.github_session import GitHubSession
    from github_wallpaper.github.repo_poller import RepoPoller
    from github_wallpaper.wallpaper.controller import WallpaperController

_logger = logging.getLogger(__name__)

_ASSETS_DIR = Path(__file__).resolve().parent / "assets"
_UNAUTHENTICATED_RATE_LIMIT = 60


def _load_asset(name: str) -> str:
    return (_ASSETS_DIR / name).read_text(encoding="utf-8")


class BridgeChannelHost(QObject):
    """QObject для QWebChannel: приём сообщений из JS."""

    def __init__(self, on_message: Callable[[str], None]) -> None:
        super().__init__()
        self._on_message = on_message

    @Slot(str)
    def receiveFromJs(self, message: str) -> None:
        self._on_message(message)


def serialize_message(message: Any) -> str:
    """Сериализует сообщение bridge в JSON (ключи уже в camelCase)."""

    def _default(value: object) -> str:
        if isinstance(value, datetime):
            return value.isoformat()
        raise TypeError(f"Object of type {type(value)!r} is not JSON serializable")

    return json.dumps(message, ensure_ascii=False, default=_default)


def install_bridge_scripts(page: QWebEnginePage, host: BridgeChannelHost) -> None:
    """Подключает QWebChannel и инжектирует qwebchannel.js + bridge-shim.js."""
    channel = QWebChannel(page)
    channel.registerObject("bridgeHost", host)
    page.setWebChannel(channel)

    scripts = page.scripts()
    for file_name in ("qwebchannel.js", "bridge-shim.js"):
        script = QWebEngineScript()
        script.setName(f"github-wallpaper-{file_name}")
        script.setSourceCode(_load_asset(file_name))
        script.setInjectionPoint(QWebEngineScript.InjectionPoint.DocumentCreation)
        script.setWorldId(QWebEngineScript.ScriptWorldId.MainWorld)
        script.setRunsOnSubFrames(False)
        scripts.insert(script)


class WallpaperBridge:
    """
    Передаёт события в страницу обоев через WallpaperController.post_message_as_json.
    Обрабатывает page:ready и open-url из JS.
    """

    def __init__(
        self,
        controller: WallpaperController,
        *,
        github_session: GitHubSession | None = None,
        repo_poller: RepoPoller | None = None,
        settings_loader: Callable[[], AppSettings] | None = None,
    ) -> None:
        self._controller = controller
        self._github_session = github_session
        self._repo_poller = repo_poller
        self._settings_loader = settings_loader or AppSettings.load
        self._host = BridgeChannelHost(self._on_js_message)
        self._started = False

    @property
    def channel_host(self) -> BridgeChannelHost:
        return self._host

    @staticmethod
    def serialize(message: Any) -> str:
        return serialize_message(message)

    def start(self) -> None:
        if self._started:
            return

        self._controller.page_ready.connect(self._on_page_ready)

        if self._repo_poller is not None:
            self._repo_poller.metadata_updated.connect(self._on_metadata_updated)
            self._repo_poller.commits_updated.connect(self._on_commits_updated)
            self._repo_poller.pulls_updated.connect(self._on_pulls_updated)
            self._repo_poller.issues_updated.connect(self._on_issues_updated)
            self._repo_poller.releases_updated.connect(self._on_releases_updated)
            self._repo_poller.ci_run_updated.connect(self._on_ci_run_updated)
            self._repo_poller.heatmap_updated.connect(self._on_heatmap_updated)
            self._repo_poller.activity_feed_updated.connect(self._on_activity_feed_updated)
            self._repo_poller.poll_failed.connect(self._on_poll_failed)
            self._repo_poller.repositories_changed.connect(self._on_repositories_changed)

        if self._github_session is not None:
            self._github_session.token_changed.connect(self._on_token_changed)

        self._started = True

    def dispose(self) -> None:
        if not self._started:
            return

        self._controller.page_ready.disconnect(self._on_page_ready)

        if self._repo_poller is not None:
            self._repo_poller.metadata_updated.disconnect(self._on_metadata_updated)
            self._repo_poller.commits_updated.disconnect(self._on_commits_updated)
            self._repo_poller.pulls_updated.disconnect(self._on_pulls_updated)
            self._repo_poller.issues_updated.disconnect(self._on_issues_updated)
            self._repo_poller.releases_updated.disconnect(self._on_releases_updated)
            self._repo_poller.ci_run_updated.disconnect(self._on_ci_run_updated)
            self._repo_poller.heatmap_updated.disconnect(self._on_heatmap_updated)
            self._repo_poller.activity_feed_updated.disconnect(self._on_activity_feed_updated)
            self._repo_poller.poll_failed.disconnect(self._on_poll_failed)
            self._repo_poller.repositories_changed.disconnect(self._on_repositories_changed)

        if self._github_session is not None:
            self._github_session.token_changed.disconnect(self._on_token_changed)

        self._started = False

    def attach_page(self, page: QWebEnginePage) -> None:
        install_bridge_scripts(page, self._host)

    def post(self, message: Any) -> None:
        self._controller.post_message_as_json(message)

    def push_initial_state(self) -> None:
        self.push_auth_status()
        self.push_layout()
        self.push_display_settings()
        self.push_repo_list()
        self.push_cached_state()

    def push_auth_status(self, *, has_token: bool | None = None, rate_limit: int | None = None) -> None:
        if has_token is None:
            has_token = self._github_session.has_token if self._github_session is not None else False

        if self._github_session is not None and rate_limit is None:
            current = self._github_session.client.rate_limit.current
            limit = current.limit if current.limit is not None else (5000 if has_token else _UNAUTHENTICATED_RATE_LIMIT)
            remaining = current.remaining if current.remaining is not None else limit
        else:
            limit = rate_limit if rate_limit is not None else (_UNAUTHENTICATED_RATE_LIMIT if not has_token else 5000)
            remaining = limit if has_token else _UNAUTHENTICATED_RATE_LIMIT

        self.post(
            {
                "type": "auth:status",
                "payload": {
                    "hasToken": has_token,
                    "rateLimit": limit,
                    "rateLimitRemaining": remaining,
                    "message": None
                    if has_token
                    else "GitHub token не задан — лимит 60 запросов/час. Добавьте PAT в Настройках.",
                },
            }
        )

    def push_layout(self) -> None:
        settings = self._settings_loader()
        self.post(
            {
                "type": "layout:update",
                "payload": {
                    "columns": settings.grid_columns,
                    "rows": settings.grid_rows,
                },
            }
        )

    def push_display_settings(self) -> None:
        settings = self._settings_loader()
        self.post(
            {
                "type": "display:update",
                "payload": settings.card_display.to_bridge_payload(),
            }
        )

    def push_repo_list(self) -> None:
        settings = self._settings_loader()
        capacity = settings.grid_columns * settings.grid_rows
        slot_slugs = list(settings.repository_slots[:capacity])

        while len(slot_slugs) < capacity:
            slot_slugs.append("")

        slots: list[dict[str, str] | None] = []
        for slug in slot_slugs:
            reference = try_parse(slug)
            if reference is None:
                slots.append(None)
            else:
                slots.append({"owner": reference.owner, "repo": reference.repo})

        self.post(
            {
                "type": "repos:init",
                "payload": slots,
                "layout": {
                    "columns": settings.grid_columns,
                    "rows": settings.grid_rows,
                },
                "display": settings.card_display.to_bridge_payload(),
            }
        )

    def _on_page_ready(self) -> None:
        self.push_initial_state()

    def _on_token_changed(self) -> None:
        self.push_auth_status()

    def _on_repositories_changed(self) -> None:
        self.push_repo_list()
        self.push_cached_state()

    def _on_metadata_updated(self, repository: RepoReference, snapshot: RepoMetadataSnapshot) -> None:
        self.post(_create_metadata_message(repository, snapshot))

    def _on_commits_updated(self, repository: RepoReference, commits: list[RepoCommitSnapshot]) -> None:
        self.post(_create_commits_message(repository, commits))

    def _on_pulls_updated(self, repository: RepoReference, pulls: list[RepoPullSnapshot]) -> None:
        self.post(_create_pulls_message(repository, pulls))

    def _on_issues_updated(self, repository: RepoReference, issues: list[RepoIssueSnapshot]) -> None:
        self.post(_create_issues_message(repository, issues))

    def _on_releases_updated(self, repository: RepoReference, releases: list[RepoReleaseSnapshot]) -> None:
        self.post(_create_releases_message(repository, releases))

    def _on_ci_run_updated(self, repository: RepoReference, run: RepoCiRunSnapshot | None) -> None:
        self.post(_create_ci_run_message(repository, run))

    def _on_heatmap_updated(self, repository: RepoReference, heatmap: RepoHeatmapSnapshot) -> None:
        self.post(_create_heatmap_message(repository, heatmap))

    def _on_activity_feed_updated(self, repository: RepoReference, feed: list[ActivityFeedItem]) -> None:
        self.post(_create_activity_feed_message(repository, feed))

    def _on_poll_failed(self, repository: RepoReference, kind: RepoPollKind, exception: BaseException) -> None:
        if kind in (RepoPollKind.HEATMAP, RepoPollKind.EVENTS):
            return
        self.post(_create_poll_failed_message(repository, kind, exception))

    def push_cached_state(self) -> None:
        if self._repo_poller is None:
            return

        for repository in self._repo_poller.repositories:
            metadata = self._repo_poller.get_cached_metadata(repository)
            if metadata is not None:
                self.post(_create_metadata_message(repository, metadata))

            commits = self._repo_poller.get_cached_commits(repository)
            if commits is not None:
                self.post(_create_commits_message(repository, commits))

            pulls = self._repo_poller.get_cached_pulls(repository)
            if pulls is not None:
                self.post(_create_pulls_message(repository, pulls))

            issues = self._repo_poller.get_cached_issues(repository)
            if issues is not None:
                self.post(_create_issues_message(repository, issues))

            releases = self._repo_poller.get_cached_releases(repository)
            if releases is not None:
                self.post(_create_releases_message(repository, releases))

            ci_run = self._repo_poller.get_cached_ci_run(repository)
            if ci_run is not None:
                self.post(_create_ci_run_message(repository, ci_run))

            heatmap = self._repo_poller.get_cached_heatmap(repository)
            if heatmap is not None:
                self.post(_create_heatmap_message(repository, heatmap))

            feed = self._repo_poller.get_cached_activity_feed(repository)
            if feed is not None:
                self.post(_create_activity_feed_message(repository, feed))

    def _on_js_message(self, raw: str) -> None:
        try:
            data = json.loads(raw)
        except json.JSONDecodeError:
            _logger.debug("Bridge: невалидный JSON от JS: %s", raw[:200])
            return

        if not isinstance(data, dict):
            return

        message_type = data.get("type")
        if message_type == "page:ready":
            self._controller.notify_page_ready()
            self.push_initial_state()
            return

        if message_type != "open-url":
            return

        url = data.get("url")
        if not isinstance(url, str) or not url.strip():
            return

        parsed = urlparse(url.strip())
        if parsed.scheme not in ("http", "https"):
            return

        QDesktopServices.openUrl(QUrl(url.strip()))


def _create_metadata_message(repository: RepoReference, snapshot: RepoMetadataSnapshot) -> dict[str, Any]:
    return {
        "type": "repo:metadata",
        "owner": repository.owner,
        "repo": repository.repo,
        "payload": {
            "fullName": snapshot.full_name,
            "description": snapshot.description,
            "stars": snapshot.stargazers_count,
            "forks": snapshot.forks_count,
            "openIssues": snapshot.open_issues_count,
            "htmlUrl": snapshot.html_url,
            "fetchedAt": snapshot.fetched_at,
        },
    }


def _create_commits_message(repository: RepoReference, commits: list[RepoCommitSnapshot]) -> dict[str, Any]:
    return {
        "type": "repo:commits",
        "owner": repository.owner,
        "repo": repository.repo,
        "payload": [
            {
                "sha": commit.sha,
                "message": commit.message,
                "authorName": commit.author_name,
                "authorDate": commit.author_date,
                "htmlUrl": commit.html_url,
            }
            for commit in commits
        ],
    }


def _create_pulls_message(repository: RepoReference, pulls: list[RepoPullSnapshot]) -> dict[str, Any]:
    return {
        "type": "repo:pulls",
        "owner": repository.owner,
        "repo": repository.repo,
        "payload": [
            {
                "number": pull.number,
                "title": pull.title,
                "userLogin": pull.user_login,
                "createdAt": pull.created_at,
                "htmlUrl": pull.html_url,
            }
            for pull in pulls
        ],
    }


def _create_issues_message(repository: RepoReference, issues: list[RepoIssueSnapshot]) -> dict[str, Any]:
    return {
        "type": "repo:issues",
        "owner": repository.owner,
        "repo": repository.repo,
        "payload": [
            {
                "number": issue.number,
                "title": issue.title,
                "userLogin": issue.user_login,
                "createdAt": issue.created_at,
                "htmlUrl": issue.html_url,
            }
            for issue in issues
        ],
    }


def _create_releases_message(repository: RepoReference, releases: list[RepoReleaseSnapshot]) -> dict[str, Any]:
    return {
        "type": "repo:releases",
        "owner": repository.owner,
        "repo": repository.repo,
        "payload": [
            {
                "id": release.id,
                "tagName": release.tag_name,
                "name": release.name,
                "isPrerelease": release.is_prerelease,
                "publishedAt": release.published_at,
                "htmlUrl": release.html_url,
            }
            for release in releases
        ],
    }


def _create_ci_run_message(repository: RepoReference, run: RepoCiRunSnapshot | None) -> dict[str, Any]:
    return {
        "type": "repo:ci-run",
        "owner": repository.owner,
        "repo": repository.repo,
        "payload": None
        if run is None
        else {
            "id": run.id,
            "name": run.name,
            "status": run.status,
            "conclusion": run.conclusion,
            "updatedAt": run.updated_at,
            "htmlUrl": run.html_url,
        },
    }


def _create_heatmap_message(repository: RepoReference, heatmap: RepoHeatmapSnapshot) -> dict[str, Any]:
    return {
        "type": "repo:heatmap",
        "owner": repository.owner,
        "repo": repository.repo,
        "payload": {
            "weeks": [{"total": week.total, "days": week.days} for week in heatmap.weeks],
            "fetchedAt": heatmap.fetched_at,
        },
    }


def _create_activity_feed_message(repository: RepoReference, feed: list[ActivityFeedItem]) -> dict[str, Any]:
    return {
        "type": "repo:activity-feed",
        "owner": repository.owner,
        "repo": repository.repo,
        "payload": [
            {
                "id": item.id,
                "kind": item.kind,
                "title": item.title,
                "subtitle": item.subtitle,
                "timestamp": item.timestamp,
                "htmlUrl": item.html_url,
                "isNew": item.is_new,
            }
            for item in feed
        ],
    }


def _create_poll_failed_message(
    repository: RepoReference,
    kind: RepoPollKind,
    exception: BaseException,
) -> dict[str, Any]:
    error = GitHubPollError.from_exception(exception)
    return {
        "type": "repo:poll-failed",
        "owner": repository.owner,
        "repo": repository.repo,
        "payload": {
            "kind": kind.name.replace("_", "").lower(),
            "code": error.code_name,
            "message": error.message,
            "hint": error.hint,
        },
    }


class PendingMessageQueue:
    """Очередь JSON-сообщений до готовности страницы."""

    def __init__(self) -> None:
        self._pending: deque[str] = deque()
        self._page_ready = False

    @property
    def page_ready(self) -> bool:
        return self._page_ready

    def reset(self) -> None:
        self._page_ready = False
        self._pending.clear()

    def mark_ready(self) -> None:
        self._page_ready = True

    def enqueue(self, json_str: str) -> None:
        self._pending.append(json_str)

    def flush(self) -> list[str]:
        batch = list(self._pending)
        self._pending.clear()
        return batch

    def should_queue(self) -> bool:
        return not self._page_ready


def deliver_json_to_page(page: QWebEnginePage, json_str: str) -> None:
    """Доставляет JSON в JS через shim (window.chrome.webview.dispatchMessage)."""
    payload = json.dumps(json_str)
    script = (
        "(function(){"
        "if(!window.chrome||!window.chrome.webview"
        "||typeof window.chrome.webview.dispatchMessage!=='function')return;"
        f"window.chrome.webview.dispatchMessage({payload});"
        "})()"
    )
    page.runJavaScript(script)

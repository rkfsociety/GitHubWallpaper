"""Тесты GitHub-слоя (парсеры, rate limit, activity, polling)."""

from __future__ import annotations

import json
import unittest
from datetime import datetime, timedelta, timezone
from unittest.mock import patch

from github_wallpaper.github.activity_aggregator import ActivityAggregator
from github_wallpaper.github.api_parsers import (
    ActivityFeedItem,
    parse_commits,
    parse_metadata,
    parse_pulls,
)
from github_wallpaper.github.github_models import GitHubRateLimit
from github_wallpaper.github.github_poll_error import GitHubPollError
from github_wallpaper.github.poll_intervals import PollIntervalPreset, for_activity_preset
from github_wallpaper.github.rate_limit_guard import RateLimitGuard
from github_wallpaper.repo_url_parser import RepoReference, parse, try_parse


class RepoUrlParserTests(unittest.TestCase):
    def test_slug_and_html_url(self) -> None:
        ref = parse("microsoft/vscode")
        self.assertEqual(ref.slug, "microsoft/vscode")
        self.assertEqual(ref.html_url, "https://github.com/microsoft/vscode")

    def test_git_suffix(self) -> None:
        ref = try_parse("https://github.com/octocat/Hello-World.git")
        assert ref is not None
        self.assertEqual(ref.repo, "Hello-World")


class PollIntervalsTests(unittest.TestCase):
    def test_normal_preset(self) -> None:
        intervals = for_activity_preset(PollIntervalPreset.NORMAL)
        self.assertEqual(intervals.metadata, timedelta(minutes=10))
        self.assertEqual(intervals.heatmap, timedelta(hours=1))


class RateLimitGuardTests(unittest.TestCase):
    def test_backoff_when_remaining_zero(self) -> None:
        guard = RateLimitGuard()
        reset_at = datetime.now(timezone.utc) + timedelta(minutes=5)
        guard.observe(GitHubRateLimit(limit=60, remaining=0, used=60, reset_at=reset_at))
        self.assertIsNotNone(guard.backoff_until)


class ActivityAggregatorTests(unittest.TestCase):
    def test_commits_bootstrap_does_not_create_feed_items(self) -> None:
        repo = RepoReference("octocat", "Hello-World")
        aggregator = ActivityAggregator()
        commits = parse_commits(_SAMPLE_COMMITS_JSON)
        feed = aggregator.integrate_commits(repo, commits)
        self.assertEqual(feed, [])

    def test_commits_delta_after_bootstrap(self) -> None:
        repo = RepoReference("octocat", "Hello-World")
        aggregator = ActivityAggregator()
        commits = parse_commits(_SAMPLE_COMMITS_JSON)
        aggregator.integrate_commits(repo, commits)

        new_commits = [
            *commits,
            type(commits[0])(
                sha="newsha123",
                message="feat: test",
                author_name="dev",
                author_date=datetime.now(timezone.utc),
                html_url="https://github.com/octocat/Hello-World/commit/newsha123",
            ),
        ]
        feed = aggregator.integrate_commits(repo, new_commits)
        self.assertEqual(len(feed), 1)
        self.assertTrue(feed[0].is_new)
        self.assertEqual(feed[0].kind, "push")


class ApiParserTests(unittest.TestCase):
    def test_parse_metadata(self) -> None:
        repo = RepoReference("microsoft", "vscode")
        snapshot = parse_metadata(repo, _SAMPLE_REPO_JSON)
        self.assertEqual(snapshot.full_name, "microsoft/vscode")
        self.assertEqual(snapshot.stargazers_count, 170000)

    def test_parse_pulls(self) -> None:
        pulls = parse_pulls(_SAMPLE_PULLS_JSON)
        self.assertEqual(len(pulls), 1)
        self.assertEqual(pulls[0].number, 42)


class CredentialStoreTests(unittest.TestCase):
    @patch("github_wallpaper.github.credential_store.keyring")
    def test_pat_roundtrip(self, keyring_mock) -> None:
        stored: dict[tuple[str, str], str] = {}

        def set_password(service: str, username: str, password: str) -> None:
            stored[(service, username)] = password

        def get_password(service: str, username: str) -> str | None:
            return stored.get((service, username))

        keyring_mock.set_password.side_effect = set_password
        keyring_mock.get_password.side_effect = get_password

        from github_wallpaper.github.credential_store import (
            PAT_SERVICE,
            PAT_USERNAME,
            GitHubPatCredentialStore,
        )

        GitHubPatCredentialStore.save("ghp_test")
        self.assertTrue(GitHubPatCredentialStore.exists())
        self.assertEqual(GitHubPatCredentialStore.read(), "ghp_test")
        self.assertEqual(keyring_mock.set_password.call_args[0], (PAT_SERVICE, PAT_USERNAME, "ghp_test"))


_SAMPLE_REPO_JSON = json.dumps(
    {
        "full_name": "microsoft/vscode",
        "description": "Visual Studio Code",
        "stargazers_count": 170000,
        "forks_count": 30000,
        "open_issues_count": 5000,
        "html_url": "https://github.com/microsoft/vscode",
    }
)

_SAMPLE_COMMITS_JSON = json.dumps(
    [
        {
            "sha": "abc123",
            "html_url": "https://github.com/octocat/Hello-World/commit/abc123",
            "commit": {
                "message": "Initial commit",
                "author": {"name": "Octocat", "date": "2024-01-01T00:00:00Z"},
            },
        }
    ]
)

_SAMPLE_PULLS_JSON = json.dumps(
    [
        {
            "number": 42,
            "title": "Fix bug",
            "user": {"login": "dev"},
            "created_at": "2024-01-01T00:00:00Z",
            "html_url": "https://github.com/octocat/Hello-World/pull/42",
        }
    ]
)


if __name__ == "__main__":
    unittest.main()

"""Тесты автообновления v2.0."""

from __future__ import annotations

import json
import unittest
from unittest.mock import patch

import httpx

from github_wallpaper.update import app_version, defaults
from github_wallpaper.update.checker import AppUpdateChecker, _parse_version_json
from github_wallpaper.update.models import Failed, Skipped, UpToDate, UpdateAvailable


class AppVersionTests(unittest.TestCase):
    def test_try_parse_version(self) -> None:
        self.assertEqual(app_version.try_parse_version("2.0.15"), (2, 0, 15))
        self.assertEqual(app_version.try_parse_version("2.0.0-beta.3"), (2, 0, 0))
        self.assertIsNone(app_version.try_parse_version(""))

    def test_is_remote_newer(self) -> None:
        with patch.object(app_version, "current_version", return_value="2.0.0"):
            self.assertTrue(app_version.is_remote_newer("2.0.1"))
            self.assertFalse(app_version.is_remote_newer("1.9.9"))

    def test_is_development_build(self) -> None:
        with patch.object(app_version, "current_version", return_value="2.0.0a1"):
            self.assertTrue(app_version.is_development_build())
        with patch.object(app_version, "current_version", return_value="2.0.5"):
            self.assertFalse(app_version.is_development_build())


class AppUpdateCheckerTests(unittest.TestCase):
    def test_parse_version_json(self) -> None:
        payload = json.dumps({"version": "2.0.42"})
        self.assertEqual(_parse_version_json(payload), "2.0.42")

    def test_skips_non_frozen_build(self) -> None:
        with patch.object(app_version, "can_self_update", return_value=False):
            with AppUpdateChecker() as checker:
                result = checker.check()
        self.assertIsInstance(result, Skipped)

    def test_uses_authorization_header_when_token_provided(self) -> None:
        with AppUpdateChecker(token="ghp_test_token") as checker:
            self.assertEqual(checker._client.headers.get("Authorization"), "Bearer ghp_test_token")

    def test_detects_update_from_release_payload(self) -> None:
        release_payload = {
            "html_url": "https://github.com/rkfsociety/GitHubWallpaper/releases/tag/latest",
            "name": "GitHubWallpaper 2.0.99",
            "assets": [
                {
                    "name": "GitHubWallpaper-Win-x64.zip",
                    "browser_download_url": "https://example.com/archive.zip",
                    "size": 12_000_000,
                },
                {
                    "name": "version.json",
                    "browser_download_url": "https://example.com/version.json",
                    "size": 128,
                },
            ],
        }

        def handler(request: httpx.Request) -> httpx.Response:
            if request.url.path.endswith("/releases/tags/latest"):
                return httpx.Response(200, json=release_payload)
            if request.url.path.endswith("/version.json"):
                return httpx.Response(200, text=json.dumps({"version": "2.0.99"}))
            return httpx.Response(404)

        transport = httpx.MockTransport(handler)

        with patch.object(app_version, "can_self_update", return_value=True):
            with patch.object(app_version, "is_development_build", return_value=False):
                with patch.object(app_version, "current_version", return_value="2.0.0"):
                    with patch(
                        "github_wallpaper.update.defaults.platform_archive_asset_name",
                        return_value="GitHubWallpaper-Win-x64.zip",
                    ):
                        client = httpx.Client(transport=transport)
                        with AppUpdateChecker(client=client) as checker:
                            result = checker.check()

        self.assertIsInstance(result, UpdateAvailable)
        assert isinstance(result, UpdateAvailable)
        self.assertEqual(result.update.version, "2.0.99")

    def test_up_to_date_when_versions_match(self) -> None:
        release_payload = {
            "name": "GitHubWallpaper 2.0.0",
            "assets": [
                {
                    "name": "GitHubWallpaper-Win-x64.zip",
                    "browser_download_url": "https://example.com/archive.zip",
                    "size": 12_000_000,
                }
            ],
        }

        transport = httpx.MockTransport(
            lambda request: httpx.Response(200, json=release_payload),
        )

        with patch.object(app_version, "can_self_update", return_value=True):
            with patch.object(app_version, "is_development_build", return_value=False):
                with patch.object(app_version, "current_version", return_value="2.0.0"):
                    with patch(
                        "github_wallpaper.update.defaults.platform_archive_asset_name",
                        return_value="GitHubWallpaper-Win-x64.zip",
                    ):
                        client = httpx.Client(transport=transport)
                        with AppUpdateChecker(client=client) as checker:
                            result = checker.check()

        self.assertIsInstance(result, UpToDate)

    def test_failed_when_asset_missing(self) -> None:
        release_payload = {"name": "GitHubWallpaper 2.0.1", "assets": []}
        transport = httpx.MockTransport(
            lambda request: httpx.Response(200, json=release_payload),
        )

        with patch.object(app_version, "can_self_update", return_value=True):
            with patch.object(app_version, "is_development_build", return_value=False):
                with patch(
                    "github_wallpaper.update.defaults.platform_archive_asset_name",
                    return_value="GitHubWallpaper-Win-x64.zip",
                ):
                    client = httpx.Client(transport=transport)
                    with AppUpdateChecker(client=client) as checker:
                        result = checker.check()

        self.assertIsInstance(result, Failed)


class UpdateDefaultsTests(unittest.TestCase):
    def test_platform_installer_asset_name_linux(self) -> None:
        with patch.object(defaults.sys, "platform", "linux"):
            self.assertEqual(defaults.platform_installer_asset_name(), "GitHubWallpaper-linux-x64")

    def test_platform_installer_asset_name_windows(self) -> None:
        with patch.object(defaults.sys, "platform", "win32"):
            self.assertEqual(defaults.platform_installer_asset_name(), "GitHubWallpaper.exe")


if __name__ == "__main__":
    unittest.main()

"""Тесты bootstrap-установки runtime."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

import httpx

from github_wallpaper.bootstrap import installer as bootstrap_installer
from github_wallpaper.bootstrap.runtime_paths import (
    launcher_path,
    runtime_root,
)
from github_wallpaper.update import app_version


class BootstrapRuntimePathsTests(unittest.TestCase):
    def test_runtime_paths(self) -> None:
        with patch("github_wallpaper.bootstrap.runtime_paths.config_dir") as config_dir:
            config_dir.return_value = Path("C:/Users/me/AppData/Roaming/GitHubWallpaper")
            self.assertEqual(
                launcher_path(),
                Path("C:/Users/me/AppData/Roaming/GitHubWallpaper/GitHubWallpaper.exe"),
            )
            self.assertEqual(
                runtime_root(),
                Path("C:/Users/me/AppData/Roaming/GitHubWallpaper/app"),
            )


class BootstrapInstallerTests(unittest.TestCase):
    def test_fetch_runtime_asset(self) -> None:
        release_payload = {
            "assets": [
                {
                    "name": "GitHubWallpaper-Win-x64.zip",
                    "browser_download_url": "https://example.com/runtime.zip",
                    "size": 12_000_000,
                }
            ]
        }
        transport = httpx.MockTransport(
            lambda request: httpx.Response(200, json=release_payload),
        )
        client = httpx.Client(transport=transport)

        with patch(
            "github_wallpaper.bootstrap.installer.defaults.platform_archive_asset_name",
            return_value="GitHubWallpaper-Win-x64.zip",
        ):
            with patch("github_wallpaper.bootstrap.installer.httpx.Client") as client_cls:
                client_cls.return_value.__enter__.return_value = client
                client_cls.return_value.__exit__.return_value = None
                url, name, size = bootstrap_installer.fetch_runtime_asset()

        self.assertEqual(url, "https://example.com/runtime.zip")
        self.assertEqual(name, "GitHubWallpaper-Win-x64.zip")
        self.assertEqual(size, 12_000_000)

    def test_extract_runtime_from_zip(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            archive_path = temp_path / "runtime.zip"
            with zipfile.ZipFile(archive_path, "w") as archive:
                archive.writestr("GitHubWallpaper/GitHubWallpaper.exe", b"runtime")

            target_dir = temp_path / "app"
            with patch("github_wallpaper.bootstrap.installer.runtime_root", return_value=target_dir):
                with patch(
                    "github_wallpaper.bootstrap.installer.update_work_directory",
                    return_value=temp_path / "work",
                ):
                    bootstrap_installer._extract_runtime(archive_path)

            self.assertTrue((target_dir / "GitHubWallpaper.exe").is_file())


class AppVersionBootstrapTests(unittest.TestCase):
    def test_bootstrap_build_cannot_self_update(self) -> None:
        with patch.object(sys, "frozen", True, create=True):
            with patch.object(app_version, "is_bootstrap_build", return_value=True):
                self.assertFalse(app_version.can_self_update())


if __name__ == "__main__":
    unittest.main()

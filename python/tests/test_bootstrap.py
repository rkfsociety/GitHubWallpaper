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
from github_wallpaper.bootstrap import runtime_version
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
                    "name": "version.json",
                    "browser_download_url": "https://example.com/version.json",
                    "size": 128,
                },
                {
                    "name": "GitHubWallpaper-Win-x64.zip",
                    "browser_download_url": "https://example.com/runtime.zip",
                    "size": 12_000_000,
                },
            ]
        }
        transport = httpx.MockTransport(
            lambda request: (
                httpx.Response(200, json={"version": "2.0.70"})
                if request.url.path.endswith("/version.json")
                else httpx.Response(200, json=release_payload)
            ),
        )
        client = httpx.Client(transport=transport)

        with patch(
            "github_wallpaper.bootstrap.installer.defaults.platform_archive_asset_name",
            return_value="GitHubWallpaper-Win-x64.zip",
        ):
            with patch("github_wallpaper.bootstrap.installer.httpx.Client") as client_cls:
                client_cls.return_value.__enter__.return_value = client
                client_cls.return_value.__exit__.return_value = None
                release = bootstrap_installer.fetch_runtime_release()

        self.assertEqual(release.version, "2.0.70")
        self.assertEqual(release.download_url, "https://example.com/runtime.zip")
        self.assertEqual(release.asset_name, "GitHubWallpaper-Win-x64.zip")
        self.assertEqual(release.asset_size_bytes, 12_000_000)

    def test_runtime_needs_update_when_marker_is_older(self) -> None:
        release_payload = {
            "assets": [
                {
                    "name": "version.json",
                    "browser_download_url": "https://example.com/version.json",
                    "size": 128,
                },
                {
                    "name": "GitHubWallpaper-Win-x64.zip",
                    "browser_download_url": "https://example.com/runtime.zip",
                    "size": 12_000_000,
                },
            ]
        }
        transport = httpx.MockTransport(
            lambda request: (
                httpx.Response(200, json={"version": "2.0.70"})
                if request.url.path.endswith("/version.json")
                else httpx.Response(200, json=release_payload)
            ),
        )
        client = httpx.Client(transport=transport)

        with patch(
            "github_wallpaper.bootstrap.installer.defaults.platform_archive_asset_name",
            return_value="GitHubWallpaper-Win-x64.zip",
        ):
            with patch("github_wallpaper.bootstrap.installer.httpx.Client") as client_cls:
                client_cls.return_value.__enter__.return_value = client
                client_cls.return_value.__exit__.return_value = None
                with patch.object(runtime_version, "read_installed_version", return_value="2.0.69"):
                    self.assertTrue(bootstrap_installer.runtime_needs_update())

    def test_runtime_does_not_need_update_when_current(self) -> None:
        release_payload = {
            "assets": [
                {
                    "name": "version.json",
                    "browser_download_url": "https://example.com/version.json",
                    "size": 128,
                },
                {
                    "name": "GitHubWallpaper-Win-x64.zip",
                    "browser_download_url": "https://example.com/runtime.zip",
                    "size": 12_000_000,
                },
            ]
        }
        transport = httpx.MockTransport(
            lambda request: (
                httpx.Response(200, json={"version": "2.0.69"})
                if request.url.path.endswith("/version.json")
                else httpx.Response(200, json=release_payload)
            ),
        )
        client = httpx.Client(transport=transport)

        with patch(
            "github_wallpaper.bootstrap.installer.defaults.platform_archive_asset_name",
            return_value="GitHubWallpaper-Win-x64.zip",
        ):
            with patch("github_wallpaper.bootstrap.installer.httpx.Client") as client_cls:
                client_cls.return_value.__enter__.return_value = client
                client_cls.return_value.__exit__.return_value = None
                with patch.object(runtime_version, "read_installed_version", return_value="2.0.69"):
                    self.assertFalse(bootstrap_installer.runtime_needs_update())

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


class BootstrapRuntimeVersionTests(unittest.TestCase):
    def test_read_installed_version_from_marker(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            config = Path(temp_dir)
            marker = config / "runtime-version.json"
            marker.write_text('{"version": "2.0.69"}\n', encoding="utf-8")
            with patch("github_wallpaper.bootstrap.runtime_version.config_dir", return_value=config):
                self.assertEqual(runtime_version.read_installed_version(), "2.0.69")

    def test_read_installed_version_from_dist_info(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            runtime_dir = Path(temp_dir) / "app"
            metadata_dir = runtime_dir / "_internal" / "github_wallpaper-2.0.0a1.dist-info"
            metadata_dir.mkdir(parents=True)
            (metadata_dir / "METADATA").write_text("Version: 2.0.0a1\n", encoding="utf-8")

            with patch("github_wallpaper.bootstrap.runtime_version.config_dir", return_value=Path(temp_dir)):
                with patch("github_wallpaper.bootstrap.runtime_version.runtime_root", return_value=runtime_dir):
                    self.assertEqual(runtime_version.read_installed_version(), "2.0.0a1")

    def test_is_version_newer_than(self) -> None:
        self.assertTrue(app_version.is_version_newer_than("2.0.70", "2.0.69"))
        self.assertFalse(app_version.is_version_newer_than("2.0.69", "2.0.70"))


class AppVersionBootstrapTests(unittest.TestCase):
    def test_bootstrap_build_cannot_self_update(self) -> None:
        with patch.object(sys, "frozen", True, create=True):
            with patch.object(app_version, "is_bootstrap_build", return_value=True):
                self.assertFalse(app_version.can_self_update())


class BootstrapEntryPointTests(unittest.TestCase):
    def test_main_py_has_script_entry_guard(self) -> None:
        main_py = Path(__file__).resolve().parents[1] / "github_wallpaper" / "bootstrap" / "main.py"
        source = main_py.read_text(encoding="utf-8")
        self.assertIn('if __name__ == "__main__":', source)
        self.assertIn("raise SystemExit(main())", source)

    def test_main_checks_runtime_update_before_launch(self) -> None:
        main_py = Path(__file__).resolve().parents[1] / "github_wallpaper" / "bootstrap" / "main.py"
        source = main_py.read_text(encoding="utf-8")
        self.assertIn("_ensure_runtime_up_to_date", source)
        self.assertIn("runtime_needs_update", source)


if __name__ == "__main__":
    unittest.main()

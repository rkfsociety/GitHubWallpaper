"""Тесты скачивания release-артефактов."""

from __future__ import annotations

import unittest

from github_wallpaper.update.asset_download import build_download_headers, resolve_download_target
from github_wallpaper.update.models import AppUpdateInfo


class AssetDownloadTests(unittest.TestCase):
    def test_resolve_download_target_prefers_api_with_asset_id(self) -> None:
        update = AppUpdateInfo(
            version="2.0.69",
            download_url="https://github.com/example/archive.zip",
            release_page_url="https://github.com/example/releases",
            asset_size_bytes=100,
            asset_name="GitHubWallpaper-Win-x64.zip",
            asset_id=12345,
        )

        url, headers = resolve_download_target(update, token="ghp_test")

        self.assertIn("/releases/assets/12345", url)
        self.assertEqual(headers["Accept"], "application/octet-stream")
        self.assertEqual(headers["Authorization"], "Bearer ghp_test")

    def test_resolve_download_target_falls_back_to_browser_url(self) -> None:
        update = AppUpdateInfo(
            version="2.0.69",
            download_url="https://github.com/example/archive.zip",
            release_page_url="https://github.com/example/releases",
            asset_size_bytes=100,
            asset_name="GitHubWallpaper-Win-x64.zip",
        )

        url, headers = resolve_download_target(update)

        self.assertEqual(url, "https://github.com/example/archive.zip")
        self.assertNotIn("Accept", headers)

    def test_build_download_headers_skips_non_ascii_token(self) -> None:
        headers = build_download_headers(token="\u6867\u6867")
        self.assertNotIn("Authorization", headers)

    def test_build_download_headers_keeps_ascii_token(self) -> None:
        headers = build_download_headers(token="ghp_ascii")
        self.assertEqual(headers["Authorization"], "Bearer ghp_ascii")


if __name__ == "__main__":
    unittest.main()

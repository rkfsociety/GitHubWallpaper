"""Параметры релизов GitHubWallpaper v2.0 на GitHub."""

from __future__ import annotations

import sys
from datetime import timedelta

OWNER = "rkfsociety"
REPO = "GitHubWallpaper"
RELEASE_TAG = "v2.0-beta"
VERSION_ASSET_NAME = "version.json"

AUTOMATIC_CHECK_INTERVAL = timedelta(hours=24)

MINIMUM_ARCHIVE_SIZE_BYTES = 5_000_000


def release_api_url() -> str:
    return f"https://api.github.com/repos/{OWNER}/{REPO}/releases/tags/{RELEASE_TAG}"


def release_page_url() -> str:
    return f"https://github.com/{OWNER}/{REPO}/releases/tag/{RELEASE_TAG}"


def platform_archive_asset_name() -> str:
    if sys.platform == "win32":
        return "GitHubWallpaper-Win-x64.zip"
    return "GitHubWallpaper-linux-x64.tar.gz"

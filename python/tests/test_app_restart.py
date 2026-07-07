"""Тесты перезапуска приложения."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path
from unittest.mock import patch

from github_wallpaper import app_restart


class AppRestartTests(unittest.TestCase):
    def test_resolve_launch_target_frozen(self) -> None:
        with patch.object(sys, "frozen", True, create=True), patch.object(
            sys, "executable", r"C:\Apps\GitHubWallpaper\GitHubWallpaper.exe"
        ):
            executable, args, work_dir = app_restart._resolve_launch_target()

        self.assertEqual(executable, Path(r"C:\Apps\GitHubWallpaper\GitHubWallpaper.exe"))
        self.assertEqual(args, [])
        self.assertEqual(work_dir, executable.parent)

    def test_resolve_launch_target_development(self) -> None:
        with patch.object(sys, "frozen", False, create=True), patch.object(
            sys, "executable", r"C:\Python311\python.exe"
        ):
            executable, args, work_dir = app_restart._resolve_launch_target()

        self.assertEqual(executable, Path(r"C:\Python311\python.exe"))
        self.assertEqual(args, ["-m", "github_wallpaper.main"])
        self.assertEqual(work_dir, Path.cwd())

    def test_format_windows_command_quotes_paths(self) -> None:
        command = app_restart._format_windows_command(
            Path(r"C:\Program Files\GitHubWallpaper\GitHubWallpaper.exe"),
            [],
        )
        self.assertEqual(command, '"C:\\Program Files\\GitHubWallpaper\\GitHubWallpaper.exe"')


if __name__ == "__main__":
    unittest.main()

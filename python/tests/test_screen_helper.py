"""Тесты ScreenHelper (разрешение монитора)."""

from __future__ import annotations

import unittest
from unittest.mock import MagicMock, patch

from PySide6.QtCore import QRect
from PySide6.QtWidgets import QApplication

from github_wallpaper.desktop.screen_helper import ScreenHelper


class ScreenHelperTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls._app = QApplication([])

    def test_format_label_includes_relative_position(self) -> None:
        primary = MagicMock()
        primary.geometry.return_value = QRect(0, 0, 2560, 1440)

        left = MagicMock()
        left.geometry.return_value = QRect(-1920, 190, 1920, 1080)

        with patch.object(ScreenHelper, "primary", return_value=primary):
            label = ScreenHelper.format_label(left, 1)

        self.assertIn("слева", label)
        self.assertIn("1920×1080", label)

    def test_resolve_win32_device_name(self) -> None:
        screen = MagicMock()
        screen.name.return_value = "VA2261 Series"
        screen.geometry.return_value = QRect(-1920, 190, 1920, 1080)

        with patch.object(ScreenHelper, "all_screens", return_value=[screen]):
            with patch.object(
                ScreenHelper,
                "win32_device_name",
                return_value=r"\\.\DISPLAY1",
            ):
                resolved = ScreenHelper.resolve(r"\\.\DISPLAY1")

        self.assertIs(resolved, screen)

    def test_resolve_falls_back_to_primary_for_unknown_name(self) -> None:
        primary = MagicMock()
        primary.name.return_value = "N32SQ-PLUS"

        with patch.object(ScreenHelper, "all_screens", return_value=[primary]):
            with patch.object(ScreenHelper, "primary", return_value=primary):
                resolved = ScreenHelper.resolve("missing-monitor")

        self.assertIs(resolved, primary)


if __name__ == "__main__":
    unittest.main()

"""Тесты SettingsStore и миграции settings.json."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from github_wallpaper.github.poll_intervals import PollIntervalPreset
from github_wallpaper.settings_store import (
    AppSettings,
    CardDisplaySettings,
    SettingsStore,
    normalize_settings,
    _default_repository,
)


class SettingsStoreTests(unittest.TestCase):
    def test_migrates_legacy_repositories_to_slots(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "settings.json"
            path.write_text(
                json.dumps(
                    {
                        "repositories": ["octocat/Hello-World", "microsoft/vscode"],
                        "gridColumns": 2,
                        "gridRows": 2,
                    }
                ),
                encoding="utf-8",
            )

            settings = SettingsStore(path).load()
            self.assertEqual(settings.repository_slots[:2], ["octocat/Hello-World", "microsoft/vscode"])
            self.assertEqual(settings.repositories, ["octocat/Hello-World", "microsoft/vscode"])

    def test_card_display_reads_csharp_field_names(self) -> None:
        display = CardDisplaySettings.from_dict(
            {
                "showDescription": False,
                "showStats": True,
                "showCi": False,
            }
        )
        self.assertFalse(display.show_description)
        self.assertTrue(display.show_stats)
        self.assertFalse(display.show_ci)

    def test_save_writes_camel_case_and_preset_index(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "settings.json"
            store = SettingsStore(path)
            settings = AppSettings(
                repository_slots=[_default_repository().slug],
                repositories=[_default_repository().slug],
                poll_interval_preset=PollIntervalPreset.ECONOMY,
                auto_start=True,
                pause_on_fullscreen=False,
            )
            store.save(settings)

            data = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(data["pollIntervalPreset"], 0)
            self.assertTrue(data["autoStart"])
            self.assertFalse(data["pauseOnFullscreen"])
            self.assertIn("showDescription", data["cardDisplay"])

    def test_normalize_ensures_default_repository(self) -> None:
        settings = normalize_settings(AppSettings(grid_columns=2, grid_rows=2, repository_slots=["", ""]))
        self.assertEqual(settings.repositories, [_default_repository().slug])

    def test_save_grid_layout(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "settings.json"
            store = SettingsStore(path)
            store.save_grid_layout(2, 1, ["a/b", ""])
            settings = store.load()
            self.assertEqual(settings.grid_columns, 2)
            self.assertEqual(settings.grid_rows, 1)
            self.assertEqual(settings.repository_slots, ["a/b", ""])
            self.assertEqual(settings.repositories, ["a/b"])


if __name__ == "__main__":
    unittest.main()

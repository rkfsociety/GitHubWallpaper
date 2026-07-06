"""Тесты авторизации через GitHub CLI."""

from __future__ import annotations

import unittest
from unittest.mock import patch

from github_wallpaper.github.gh_cli_auth import (
    GhCliAuthStatus,
    get_status,
    import_token,
    read_token,
)


class GhCliAuthTests(unittest.TestCase):
    @patch("github_wallpaper.github.gh_cli_auth.find_gh_executable", return_value=None)
    def test_status_when_gh_missing(self, _find_mock) -> None:
        status = get_status()
        self.assertFalse(status.available)
        self.assertFalse(status.logged_in)
        self.assertIn("не найден", status.message or "")

    @patch("github_wallpaper.github.gh_cli_auth._run_gh")
    @patch("github_wallpaper.github.gh_cli_auth.find_gh_executable", return_value="gh")
    def test_status_when_logged_in(self, _find_mock, run_mock) -> None:
        run_mock.return_value = (
            0,
            (
                "github.com\n"
                "  ✓ Logged in to github.com account octocat (keyring)\n"
                "  - Token scopes: 'repo', 'read:user'\n"
            ),
            "",
        )

        status = get_status()
        self.assertTrue(status.available)
        self.assertTrue(status.logged_in)
        self.assertEqual(status.username, "octocat")
        self.assertEqual(status.scopes, ("repo", "read:user"))

    @patch("github_wallpaper.github.gh_cli_auth._run_gh")
    @patch("github_wallpaper.github.gh_cli_auth.find_gh_executable", return_value="gh")
    def test_status_when_not_logged_in(self, _find_mock, run_mock) -> None:
        run_mock.return_value = (
            1,
            "",
            "You are not logged into any GitHub hosts. To log in, run: gh auth login",
        )

        status = get_status()
        self.assertTrue(status.available)
        self.assertFalse(status.logged_in)
        self.assertIn("not logged", status.message or "")

    @patch("github_wallpaper.github.gh_cli_auth._run_gh")
    @patch("github_wallpaper.github.gh_cli_auth.find_gh_executable", return_value="gh")
    def test_read_token(self, _find_mock, run_mock) -> None:
        run_mock.return_value = (0, "gho_test_token\n", "")

        self.assertEqual(read_token(), "gho_test_token")

    @patch("github_wallpaper.github.gh_cli_auth.read_token", return_value="gho_test_token")
    @patch("github_wallpaper.github.gh_cli_auth.get_status")
    def test_import_token(self, status_mock, _read_mock) -> None:
        status_mock.return_value = GhCliAuthStatus(
            available=True,
            logged_in=True,
            username="octocat",
        )

        self.assertEqual(import_token(), "gho_test_token")


if __name__ == "__main__":
    unittest.main()

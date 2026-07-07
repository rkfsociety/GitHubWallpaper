"""Тесты хранилища PAT (в т.ч. миграция токена с C# v1.0)."""

from __future__ import annotations

import unittest
from unittest.mock import patch

from github_wallpaper.github import credential_store
from github_wallpaper.github.credential_store import (
    PAT_SERVICE,
    PAT_USERNAME,
    GitHubPatCredentialStore,
    _recover_legacy_token,
)


class RecoverLegacyTokenTests(unittest.TestCase):
    def test_recovers_utf8_read_as_utf16(self) -> None:
        original = "gho_" + "a" * 36
        legacy = original.encode("ascii").decode("utf-16-le")
        self.assertFalse(legacy.isascii())
        self.assertEqual(_recover_legacy_token(legacy), original)

    def test_returns_none_for_unrecoverable(self) -> None:
        self.assertIsNone(_recover_legacy_token("ключ"))


class GitHubPatCredentialStoreReadTests(unittest.TestCase):
    def test_ascii_token_passthrough(self) -> None:
        with patch.object(credential_store.keyring, "get_password", return_value="ghp_ascii"):
            self.assertEqual(GitHubPatCredentialStore.read(), "ghp_ascii")

    def test_none_when_empty(self) -> None:
        with patch.object(credential_store.keyring, "get_password", return_value="  "):
            self.assertIsNone(GitHubPatCredentialStore.read())

    def test_recovers_and_persists_legacy_token(self) -> None:
        original = "gho_" + "b" * 36
        legacy = original.encode("ascii").decode("utf-16-le")

        with patch.object(credential_store.keyring, "get_password", return_value=legacy):
            with patch.object(credential_store.keyring, "set_password") as set_password:
                result = GitHubPatCredentialStore.read()

        self.assertEqual(result, original)
        set_password.assert_called_once_with(PAT_SERVICE, PAT_USERNAME, original)


if __name__ == "__main__":
    unittest.main()

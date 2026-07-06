"""Жизненный цикл GitHub API-клиента с PAT из keyring (GitHubSession.cs)."""

from __future__ import annotations

from PySide6.QtCore import QObject, Signal

from github_wallpaper.github.credential_store import GitHubPatCredentialStore
from github_wallpaper.github.github_client import GitHubClient


class GitHubSession(QObject):
    """Управляет токеном и GitHubClient."""

    token_changed = Signal()

    def __init__(self) -> None:
        super().__init__()
        self._client = GitHubClient(GitHubPatCredentialStore.read())

    @property
    def client(self) -> GitHubClient:
        return self._client

    @property
    def has_stored_token(self) -> bool:
        return GitHubPatCredentialStore.exists()

    @property
    def has_token(self) -> bool:
        return self._client.has_token

    def reload_token_from_store(self) -> None:
        self._client.set_token(GitHubPatCredentialStore.read())
        self.token_changed.emit()

    def save_token(self, token: str) -> None:
        GitHubPatCredentialStore.save(token)
        self._client.set_token(token)
        self.token_changed.emit()

    def clear_token(self) -> None:
        GitHubPatCredentialStore.delete()
        self._client.set_token(None)
        self.token_changed.emit()

    def dispose(self) -> None:
        pass

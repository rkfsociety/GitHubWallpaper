"""Жизненный цикл GitHub API-клиента с PAT из keyring (GitHubSession.cs)."""

from __future__ import annotations

from PySide6.QtCore import QObject, Signal

from github_wallpaper.github.credential_store import GitHubPatCredentialStore
from github_wallpaper.github.gh_cli_auth import GhCliAuthError, import_token, login_web, read_token
from github_wallpaper.github.github_client import GitHubClient


class GitHubSession(QObject):
    """Управляет токеном и GitHubClient."""

    token_changed = Signal()

    def __init__(self) -> None:
        super().__init__()
        self._token_from_gh = False
        self._client = GitHubClient(self._resolve_initial_token())

    @property
    def client(self) -> GitHubClient:
        return self._client

    @property
    def has_stored_token(self) -> bool:
        return GitHubPatCredentialStore.exists()

    @property
    def uses_gh_token(self) -> bool:
        return self._token_from_gh and self._client.has_token

    @property
    def has_token(self) -> bool:
        return self._client.has_token

    def reload_token_from_store(self) -> None:
        self._set_client_token(GitHubPatCredentialStore.read(), from_gh=False)
        self.token_changed.emit()

    def save_token(self, token: str) -> None:
        GitHubPatCredentialStore.save(token)
        self._set_client_token(token, from_gh=False)
        self.token_changed.emit()

    def clear_token(self) -> None:
        GitHubPatCredentialStore.delete()
        self._set_client_token(None, from_gh=False)
        self.token_changed.emit()

    def import_token_from_gh(self) -> None:
        token = import_token()
        self.save_token(token)

    def login_with_gh(self) -> None:
        login_web()
        self.import_token_from_gh()

    def reload_token_from_gh(self) -> bool:
        if GitHubPatCredentialStore.exists():
            return False
        token = read_token()
        if not token:
            self._set_client_token(None, from_gh=False)
            return False
        self._set_client_token(token, from_gh=True)
        self.token_changed.emit()
        return True

    def _resolve_initial_token(self) -> str | None:
        stored = GitHubPatCredentialStore.read()
        if stored:
            self._token_from_gh = False
            return stored
        gh_token = read_token()
        self._token_from_gh = gh_token is not None
        return gh_token

    def _set_client_token(self, token: str | None, *, from_gh: bool) -> None:
        self._token_from_gh = from_gh and bool(token)
        self._client.set_token(token)

    def dispose(self) -> None:
        pass

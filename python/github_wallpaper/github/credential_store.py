"""Хранение PAT и OAuth Client Secret через keyring."""

from __future__ import annotations

import keyring
import keyring.errors

PAT_SERVICE = "GitHubWallpaper/PersonalAccessToken"
PAT_USERNAME = "GitHubPersonalAccessToken"

OAUTH_SECRET_SERVICE = "GitHubWallpaper/OAuthClientSecret"
OAUTH_SECRET_USERNAME = "GitHubOAuthClientSecret"


class GitHubPatCredentialStore:
    """PAT в keyring (имена как в GitHubPatCredentialStore.cs)."""

    @staticmethod
    def read() -> str | None:
        try:
            return keyring.get_password(PAT_SERVICE, PAT_USERNAME)
        except keyring.errors.KeyringError:
            return None

    @staticmethod
    def save(token: str) -> None:
        if not token or not token.strip():
            raise ValueError("PAT не может быть пустым.")
        keyring.set_password(PAT_SERVICE, PAT_USERNAME, token.strip())

    @staticmethod
    def delete() -> None:
        try:
            keyring.delete_password(PAT_SERVICE, PAT_USERNAME)
        except keyring.errors.PasswordDeleteError:
            pass

    @staticmethod
    def exists() -> bool:
        return GitHubPatCredentialStore.read() is not None


class GitHubOAuthClientSecretCredentialStore:
    """OAuth Client Secret в keyring."""

    @staticmethod
    def read() -> str | None:
        try:
            return keyring.get_password(OAUTH_SECRET_SERVICE, OAUTH_SECRET_USERNAME)
        except keyring.errors.KeyringError:
            return None

    @staticmethod
    def save(client_secret: str) -> None:
        if not client_secret or not client_secret.strip():
            raise ValueError("Client Secret не может быть пустым.")
        keyring.set_password(OAUTH_SECRET_SERVICE, OAUTH_SECRET_USERNAME, client_secret.strip())

    @staticmethod
    def delete() -> None:
        try:
            keyring.delete_password(OAUTH_SECRET_SERVICE, OAUTH_SECRET_USERNAME)
        except keyring.errors.PasswordDeleteError:
            pass

    @staticmethod
    def exists() -> bool:
        return GitHubOAuthClientSecretCredentialStore.read() is not None

"""Параметры OAuth-приложения GitHub (GitHubOAuthDefaults.cs)."""

from __future__ import annotations

import os

EMBEDDED_CLIENT_ID = ""
SCOPE = "repo read:user"
LOOPBACK_PORT = 8791
LOOPBACK_PATH = "/callback"
AUTHORIZE_URL = "https://github.com/login/oauth/authorize"
ACCESS_TOKEN_URL = "https://github.com/login/oauth/access_token"
DEVICE_CODE_URL = "https://github.com/login/device/code"
DEVICE_VERIFICATION_URL = "https://github.com/login/device"
REGISTRATION_URL = "https://github.com/settings/applications/new"


def redirect_uri() -> str:
    return f"http://127.0.0.1:{LOOPBACK_PORT}{LOOPBACK_PATH}"


def resolve_client_id(settings_client_id: str | None = None) -> str | None:
    from_environment = os.environ.get("GITHUBWALLPAPER_OAUTH_CLIENT_ID")
    if from_environment and from_environment.strip():
        return from_environment.strip()

    if settings_client_id and settings_client_id.strip():
        return settings_client_id.strip()

    if EMBEDDED_CLIENT_ID.strip():
        return EMBEDDED_CLIENT_ID.strip()
    return None


def resolve_client_secret(stored_client_secret: str | None = None) -> str | None:
    from_environment = os.environ.get("GITHUBWALLPAPER_OAUTH_CLIENT_SECRET")
    if from_environment and from_environment.strip():
        return from_environment.strip()

    if stored_client_secret and stored_client_secret.strip():
        return stored_client_secret.strip()
    return None

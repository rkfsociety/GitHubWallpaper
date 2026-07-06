"""Единая точка входа OAuth GitHub (GitHubOAuthService.cs)."""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Callable

from github_wallpaper.github.oauth import defaults
from github_wallpaper.github.oauth.device_auth import GitHubDeviceAuthorization, GitHubOAuthDeviceAuth
from github_wallpaper.github.oauth.exceptions import GitHubOAuthException
from github_wallpaper.github.oauth.web_auth import GitHubOAuthWebAuth


class GitHubOAuthMethod(Enum):
    WEB_BROWSER = "webBrowser"
    DEVICE_FLOW = "deviceFlow"


@dataclass(frozen=True, slots=True)
class SignInResult:
    access_token: str
    method: GitHubOAuthMethod
    user_code: str | None = None


class GitHubOAuthService:
    def __init__(
        self,
        *,
        settings_client_id: str | None = None,
        stored_client_secret: str | None = None,
        open_url: Callable[[str], None] | None = None,
    ) -> None:
        self._client_id = defaults.resolve_client_id(settings_client_id)
        self._client_secret = defaults.resolve_client_secret(stored_client_secret)
        self._web_auth = GitHubOAuthWebAuth(open_url=open_url)
        self._device_auth = GitHubOAuthDeviceAuth(open_url=open_url)

    async def sign_in(
        self,
        progress: Callable[[str], None] | None = None,
    ) -> SignInResult:
        client_id = self._require_client_id()

        if progress:
            progress("Открываю github.com в браузере…")

        try:
            token = await self._web_auth.sign_in(client_id, self._client_secret)
            return SignInResult(token, GitHubOAuthMethod.WEB_BROWSER)
        except GitHubOAuthException as web_ex:
            if not _should_fallback_to_device_flow(web_ex):
                raise
            if progress:
                progress("Переключаюсь на вход по коду устройства…")
            return await self.sign_in_with_device_flow(progress)

    async def sign_in_with_device_flow(
        self,
        progress: Callable[[str], None] | None = None,
    ) -> SignInResult:
        client_id = self._require_client_id()
        authorization = await self._device_auth.request_device_code(client_id)
        if progress:
            progress(f"Введите на GitHub код: {authorization.user_code}")
        token = await self._device_auth.poll_for_access_token(client_id, authorization, progress)
        return SignInResult(token, GitHubOAuthMethod.DEVICE_FLOW, authorization.user_code)

    async def aclose(self) -> None:
        await self._web_auth.aclose()
        await self._device_auth.aclose()

    def _require_client_id(self) -> str:
        if self._client_id:
            return self._client_id
        raise GitHubOAuthException(
            "OAuth client_id не настроен. Создайте OAuth App на GitHub, включите Device Flow, "
            "укажите callback http://127.0.0.1:8791/callback и сохраните Client ID в настройках."
        )


def _should_fallback_to_device_flow(exception: GitHubOAuthException) -> bool:
    message = str(exception).lower()
    return (
        "локальный порт" in message
        or "браузер" in message
        or "время ожидания callback" in message
        or "client_id and/or client_secret" in message
        or "incorrect_client_credentials" in message
    )

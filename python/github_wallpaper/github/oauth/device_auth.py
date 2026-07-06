"""OAuth Device Authorization Grant (GitHubOAuthDeviceAuth.cs)."""

from __future__ import annotations

import asyncio
import json
import webbrowser
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Callable

import httpx

from github_wallpaper.github.oauth import defaults
from github_wallpaper.github.oauth.exceptions import GitHubOAuthException


@dataclass(frozen=True, slots=True)
class GitHubDeviceAuthorization:
    device_code: str
    user_code: str
    verification_uri: str
    expires_in: int
    interval: int


class GitHubOAuthDeviceAuth:
    def __init__(
        self,
        *,
        client: httpx.AsyncClient | None = None,
        open_url: Callable[[str], None] | None = None,
    ) -> None:
        self._owns_client = client is None
        self._client = client or httpx.AsyncClient(timeout=httpx.Timeout(30.0))
        self._open_url = open_url or webbrowser.open

    async def request_device_code(self, client_id: str) -> GitHubDeviceAuthorization:
        response = await self._client.post(
            defaults.DEVICE_CODE_URL,
            json={"client_id": client_id, "scope": defaults.SCOPE},
            headers={"Accept": "application/json"},
        )
        body = response.text

        if response.status_code >= 400:
            raise GitHubOAuthException(
                f"Не удалось запросить код устройства (HTTP {response.status_code}). "
                "Убедитесь, что в OAuth App включён Device Flow."
            )

        data = json.loads(body)
        device_code = data.get("device_code")
        user_code = data.get("user_code")
        if not device_code or not user_code:
            raise GitHubOAuthException("GitHub не вернул device_code или user_code.")

        verification_uri = data.get("verification_uri") or defaults.DEVICE_VERIFICATION_URL
        authorization = GitHubDeviceAuthorization(
            device_code=str(device_code),
            user_code=str(user_code),
            verification_uri=str(verification_uri),
            expires_in=int(data.get("expires_in") or 0),
            interval=int(data.get("interval") or 0),
        )
        self._open_url(authorization.verification_uri)
        return authorization

    async def poll_for_access_token(
        self,
        client_id: str,
        authorization: GitHubDeviceAuthorization,
        progress: Callable[[str], None] | None = None,
    ) -> str:
        interval = max(authorization.interval, 5)
        deadline = datetime.now(timezone.utc) + timedelta(seconds=max(authorization.expires_in, 60))

        while datetime.now(timezone.utc) < deadline:
            await asyncio.sleep(interval)

            response = await self._client.post(
                defaults.ACCESS_TOKEN_URL,
                json={
                    "client_id": client_id,
                    "device_code": authorization.device_code,
                    "grant_type": "urn:ietf:params:oauth:grant-type:device_code",
                },
                headers={"Accept": "application/json"},
            )
            data = json.loads(response.text)

            token = data.get("access_token")
            if isinstance(token, str) and token.strip():
                return token.strip()

            error = data.get("error")
            if error == "authorization_pending":
                if progress:
                    progress("Ожидание подтверждения на GitHub…")
                continue
            if error == "slow_down":
                interval += 5
                if progress:
                    progress("GitHub просит замедлить опрос…")
                continue
            if error == "expired_token":
                raise GitHubOAuthException(
                    "Код устройства истёк. Нажмите «Войти через GitHub» ещё раз."
                )
            if error == "access_denied":
                raise GitHubOAuthException("Авторизация отклонена на GitHub.")

            description = data.get("error_description") or error
            raise GitHubOAuthException(
                description or "Не удалось получить токен через Device Flow."
            )

        raise GitHubOAuthException(
            "Время ожидания авторизации истекло. Повторите вход через GitHub."
        )

    async def aclose(self) -> None:
        if self._owns_client:
            await self._client.aclose()

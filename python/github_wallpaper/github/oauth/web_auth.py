"""OAuth Authorization Code + PKCE через loopback (GitHubOAuthWebAuth.cs)."""

from __future__ import annotations

import json
import threading
import webbrowser
from http.server import BaseHTTPRequestHandler, HTTPServer
from typing import Callable
from urllib.parse import parse_qs, quote, urlparse

import httpx

from github_wallpaper.github.oauth import defaults
from github_wallpaper.github.oauth.exceptions import GitHubOAuthException
from github_wallpaper.github.oauth.pkce import create_authorization_request

_SUCCESS_HTML = """<!DOCTYPE html>
<html lang="ru">
<head><meta charset="utf-8"><title>GitHub Wallpaper</title></head>
<body style="font-family:Segoe UI,sans-serif;background:#0d1117;color:#e6edf3;text-align:center;padding:48px">
  <h1>Авторизация успешна</h1>
  <p>Можно закрыть эту вкладку и вернуться в GitHub Wallpaper.</p>
</body>
</html>"""

_FAILURE_HTML = """<!DOCTYPE html>
<html lang="ru">
<head><meta charset="utf-8"><title>GitHub Wallpaper</title></head>
<body style="font-family:Segoe UI,sans-serif;background:#0d1117;color:#e6edf3;text-align:center;padding:48px">
  <h1>Ошибка авторизации</h1>
  <p>Закройте вкладку и повторите вход в настройках приложения.</p>
</body>
</html>"""


class GitHubOAuthWebAuth:
    def __init__(
        self,
        *,
        client: httpx.AsyncClient | None = None,
        open_url: Callable[[str], None] | None = None,
    ) -> None:
        self._owns_client = client is None
        self._client = client or httpx.AsyncClient(timeout=httpx.Timeout(30.0))
        self._open_url = open_url or webbrowser.open

    async def sign_in(
        self,
        client_id: str,
        client_secret: str | None = None,
    ) -> str:
        verifier, challenge, state = create_authorization_request()
        authorize_uri = _build_authorize_uri(client_id, challenge, state)

        try:
            query, success = await _wait_for_callback(state, authorize_uri, self._open_url)
        except OSError as ex:
            raise GitHubOAuthException(
                f"Не удалось открыть локальный порт {defaults.LOOPBACK_PORT} для callback. "
                "Попробуйте вход через код устройства."
            ) from ex

        if query.get("state", [None])[0] != state:
            raise GitHubOAuthException("Неверный параметр state — авторизация отклонена.")

        if query.get("error"):
            description = query.get("error_description", query["error"])[0]
            raise GitHubOAuthException(
                description or f"GitHub отклонил авторизацию: {query['error'][0]}."
            )

        code_values = query.get("code")
        if not code_values or not code_values[0].strip():
            raise GitHubOAuthException("GitHub не вернул код авторизации.")

        if not success:
            raise GitHubOAuthException("GitHub не вернул код авторизации.")

        return await self._exchange_code(client_id, code_values[0], verifier, client_secret)

    async def _exchange_code(
        self,
        client_id: str,
        code: str,
        verifier: str,
        client_secret: str | None,
    ) -> str:
        payload: dict[str, str] = {
            "client_id": client_id,
            "redirect_uri": defaults.redirect_uri(),
            "code": code,
            "code_verifier": verifier,
        }
        if client_secret:
            payload["client_secret"] = client_secret.strip()

        response = await self._client.post(
            defaults.ACCESS_TOKEN_URL,
            json=payload,
            headers={"Accept": "application/json"},
        )

        if response.status_code >= 400:
            raise GitHubOAuthException(
                f"Не удалось обменять код на токен (HTTP {response.status_code})."
            )

        data = json.loads(response.text)
        if data.get("error"):
            description = data.get("error_description") or data.get("error")
            raise GitHubOAuthException(description or "GitHub отклонил обмен кода на токен.")

        token = data.get("access_token")
        if not isinstance(token, str) or not token.strip():
            raise GitHubOAuthException("GitHub не вернул access_token.")
        return token.strip()

    async def aclose(self) -> None:
        if self._owns_client:
            await self._client.aclose()


def _build_authorize_uri(client_id: str, challenge: str, state: str) -> str:
    query = "&".join(
        (
            f"client_id={quote(client_id)}",
            f"redirect_uri={quote(defaults.redirect_uri())}",
            f"scope={quote(defaults.SCOPE)}",
            f"state={quote(state)}",
            f"code_challenge={quote(challenge)}",
            "code_challenge_method=S256",
        )
    )
    return f"{defaults.AUTHORIZE_URL}?{query}"


async def _wait_for_callback(
    expected_state: str,
    authorize_uri: str,
    open_url: Callable[[str], None],
) -> tuple[dict[str, list[str]], bool]:
    result: dict[str, list[str]] = {}
    success = False
    done = threading.Event()

    class CallbackHandler(BaseHTTPRequestHandler):
        def do_GET(self) -> None:
            parsed = urlparse(self.path)
            if parsed.path.rstrip("/") != defaults.LOOPBACK_PATH.rstrip("/"):
                self.send_response(404)
                self.end_headers()
                return

            nonlocal result, success
            result = parse_qs(parsed.query)
            success = (
                result.get("state", [None])[0] == expected_state
                and bool(result.get("code"))
                and not result.get("error")
            )
            body = (_SUCCESS_HTML if success else _FAILURE_HTML).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            done.set()

        def log_message(self, format: str, *args: object) -> None:
            return

    server = HTTPServer(("127.0.0.1", defaults.LOOPBACK_PORT), CallbackHandler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()

    try:
        open_url(authorize_uri)
        if not done.wait(timeout=300):
            raise GitHubOAuthException("Время ожидания callback истекло.")
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=2)

    return result, success

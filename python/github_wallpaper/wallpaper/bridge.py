"""JSON-мост C# ↔ JS: сериализация camelCase, очередь до page:ready, open-url."""

from __future__ import annotations

import json
import logging
from collections import deque
from collections.abc import Callable
from pathlib import Path
from typing import TYPE_CHECKING, Any
from urllib.parse import urlparse

from PySide6.QtCore import QObject, Slot
from PySide6.QtGui import QDesktopServices
from PySide6.QtCore import QUrl
from PySide6.QtWebChannel import QWebChannel
from PySide6.QtWebEngineCore import QWebEnginePage, QWebEngineScript

from github_wallpaper.repo_url_parser import try_parse
from github_wallpaper.settings_store import AppSettings, CardDisplaySettings

if TYPE_CHECKING:
    from github_wallpaper.wallpaper.controller import WallpaperController

_logger = logging.getLogger(__name__)

_ASSETS_DIR = Path(__file__).resolve().parent / "assets"
_UNAUTHENTICATED_RATE_LIMIT = 60


def _load_asset(name: str) -> str:
    return (_ASSETS_DIR / name).read_text(encoding="utf-8")


class BridgeChannelHost(QObject):
    """QObject для QWebChannel: приём сообщений из JS."""

    def __init__(self, on_message: Callable[[str], None]) -> None:
        super().__init__()
        self._on_message = on_message

    @Slot(str)
    def receiveFromJs(self, message: str) -> None:
        self._on_message(message)


def serialize_message(message: Any) -> str:
    """Сериализует сообщение bridge в JSON (ключи уже в camelCase)."""
    return json.dumps(message, ensure_ascii=False)


def install_bridge_scripts(page: QWebEnginePage, host: BridgeChannelHost) -> None:
    """Подключает QWebChannel и инжектирует qwebchannel.js + bridge-shim.js."""
    channel = QWebChannel(page)
    channel.registerObject("bridgeHost", host)
    page.setWebChannel(channel)

    scripts = page.scripts()
    for file_name in ("qwebchannel.js", "bridge-shim.js"):
        script = QWebEngineScript()
        script.setName(f"github-wallpaper-{file_name}")
        script.setSourceCode(_load_asset(file_name))
        script.setInjectionPoint(QWebEngineScript.InjectionPoint.DocumentCreation)
        script.setWorldId(QWebEngineScript.ScriptWorldId.MainWorld)
        script.setRunsOnSubFrames(False)
        scripts.insert(script)


class WallpaperBridge:
    """
    Передаёт события в страницу обоев через WallpaperController.post_message_as_json.
    Обрабатывает page:ready и open-url из JS.
    """

    def __init__(
        self,
        controller: WallpaperController,
        settings_loader: Callable[[], AppSettings] | None = None,
    ) -> None:
        self._controller = controller
        self._settings_loader = settings_loader or AppSettings.load
        self._host = BridgeChannelHost(self._on_js_message)
        self._started = False

    @property
    def channel_host(self) -> BridgeChannelHost:
        return self._host

    @staticmethod
    def serialize(message: Any) -> str:
        return serialize_message(message)

    def start(self) -> None:
        if self._started:
            return

        self._controller.page_ready.connect(self._on_page_ready)
        self._started = True

    def dispose(self) -> None:
        if not self._started:
            return

        self._controller.page_ready.disconnect(self._on_page_ready)
        self._started = False

    def attach_page(self, page: QWebEnginePage) -> None:
        install_bridge_scripts(page, self._host)

    def post(self, message: Any) -> None:
        self._controller.post_message_as_json(message)

    def push_initial_state(self) -> None:
        self.push_auth_status()
        self.push_layout()
        self.push_display_settings()
        self.push_repo_list()

    def push_auth_status(self, *, has_token: bool = False, rate_limit: int | None = None) -> None:
        limit = rate_limit if rate_limit is not None else (_UNAUTHENTICATED_RATE_LIMIT if not has_token else 5000)
        remaining = limit if has_token else _UNAUTHENTICATED_RATE_LIMIT

        self.post(
            {
                "type": "auth:status",
                "payload": {
                    "hasToken": has_token,
                    "rateLimit": limit,
                    "rateLimitRemaining": remaining,
                    "message": None
                    if has_token
                    else "GitHub token не задан — лимит 60 запросов/час. Добавьте PAT в Настройках.",
                },
            }
        )

    def push_layout(self) -> None:
        settings = self._settings_loader()
        self.post(
            {
                "type": "layout:update",
                "payload": {
                    "columns": settings.grid_columns,
                    "rows": settings.grid_rows,
                },
            }
        )

    def push_display_settings(self) -> None:
        settings = self._settings_loader()
        self.post(
            {
                "type": "display:update",
                "payload": settings.card_display.to_bridge_payload(),
            }
        )

    def push_repo_list(self) -> None:
        settings = self._settings_loader()
        capacity = settings.grid_columns * settings.grid_rows
        slot_slugs = list(settings.repository_slots[:capacity])

        while len(slot_slugs) < capacity:
            slot_slugs.append("")

        slots: list[dict[str, str] | None] = []
        for slug in slot_slugs:
            reference = try_parse(slug)
            if reference is None:
                slots.append(None)
            else:
                slots.append({"owner": reference.owner, "repo": reference.repo})

        self.post(
            {
                "type": "repos:init",
                "payload": slots,
                "layout": {
                    "columns": settings.grid_columns,
                    "rows": settings.grid_rows,
                },
                "display": settings.card_display.to_bridge_payload(),
            }
        )

    def _on_page_ready(self) -> None:
        self.push_initial_state()

    def _on_js_message(self, raw: str) -> None:
        try:
            data = json.loads(raw)
        except json.JSONDecodeError:
            _logger.debug("Bridge: невалидный JSON от JS: %s", raw[:200])
            return

        if not isinstance(data, dict):
            return

        message_type = data.get("type")
        if message_type == "page:ready":
            self._controller.notify_page_ready()
            self.push_initial_state()
            return

        if message_type != "open-url":
            return

        url = data.get("url")
        if not isinstance(url, str) or not url.strip():
            return

        parsed = urlparse(url.strip())
        if parsed.scheme not in ("http", "https"):
            return

        QDesktopServices.openUrl(QUrl(url.strip()))


class PendingMessageQueue:
    """Очередь JSON-сообщений до готовности страницы."""

    def __init__(self) -> None:
        self._pending: deque[str] = deque()
        self._page_ready = False

    @property
    def page_ready(self) -> bool:
        return self._page_ready

    def reset(self) -> None:
        self._page_ready = False
        self._pending.clear()

    def mark_ready(self) -> None:
        self._page_ready = True

    def enqueue(self, json_str: str) -> None:
        self._pending.append(json_str)

    def flush(self) -> list[str]:
        batch = list(self._pending)
        self._pending.clear()
        return batch

    def should_queue(self) -> bool:
        return not self._page_ready


def deliver_json_to_page(page: QWebEnginePage, json_str: str) -> None:
    """Доставляет JSON в JS через shim (window.chrome.webview.dispatchMessage)."""
    payload = json.dumps(json_str)
    script = (
        "(function(){"
        "if(!window.chrome||!window.chrome.webview"
        "||typeof window.chrome.webview.dispatchMessage!=='function')return;"
        f"window.chrome.webview.dispatchMessage({payload});"
        "})()"
    )
    page.runJavaScript(script)

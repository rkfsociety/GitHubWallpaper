"""Тесты JSON-моста (сериализация, очередь, WebEngine + статичная страница)."""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

from github_wallpaper.repo_url_parser import try_parse
from github_wallpaper.settings_store import AppSettings, CardDisplaySettings
from github_wallpaper.wallpaper.bridge import (
    PendingMessageQueue,
    WallpaperBridge,
    deliver_json_to_page,
    serialize_message,
)

_ASSETS = Path(__file__).resolve().parents[1] / "github_wallpaper" / "wallpaper" / "assets"


class SerializeTests(unittest.TestCase):
    def test_auth_status_camel_case(self) -> None:
        payload = {
            "type": "auth:status",
            "payload": {
                "hasToken": False,
                "rateLimit": 60,
                "rateLimitRemaining": 60,
                "message": "test",
            },
        }
        raw = serialize_message(payload)
        data = json.loads(raw)
        self.assertEqual(data["type"], "auth:status")
        self.assertIn("hasToken", data["payload"])
        self.assertNotIn("has_token", data["payload"])

    def test_repos_init_shape(self) -> None:
        bridge = WallpaperBridge(_FakeController(), lambda: _sample_settings())
        bridge.push_repo_list()
        message = _FakeController.last_message
        assert message is not None
        self.assertEqual(message["type"], "repos:init")
        self.assertEqual(message["layout"]["columns"], 2)
        self.assertEqual(message["display"]["pullRequests"], True)
        self.assertEqual(message["payload"][0], {"owner": "octocat", "repo": "Hello-World"})


class PendingQueueTests(unittest.TestCase):
    def test_queue_until_ready(self) -> None:
        queue = PendingMessageQueue()
        self.assertTrue(queue.should_queue())
        queue.enqueue('{"type":"auth:status"}')
        queue.mark_ready()
        self.assertFalse(queue.should_queue())
        batch = queue.flush()
        self.assertEqual(batch, ['{"type":"auth:status"}'])
        self.assertEqual(queue.flush(), [])

    def test_reset_clears_state(self) -> None:
        queue = PendingMessageQueue()
        queue.mark_ready()
        queue.enqueue("x")
        queue.reset()
        self.assertFalse(queue.page_ready)
        self.assertEqual(queue.flush(), [])


class RepoParserTests(unittest.TestCase):
    def test_owner_repo_slug(self) -> None:
        ref = try_parse("octocat/Hello-World")
        assert ref is not None
        self.assertEqual(ref.owner, "octocat")
        self.assertEqual(ref.repo, "Hello-World")

    def test_github_url(self) -> None:
        ref = try_parse("https://github.com/microsoft/vscode")
        assert ref is not None
        self.assertEqual(ref.owner, "microsoft")


class _FakeController:
    last_message: dict | None = None

    def __init__(self) -> None:
        self.page_ready = _FakeSignal()

    def post_message_as_json(self, message: dict) -> None:
        _FakeController.last_message = message

    def notify_page_ready(self) -> None:
        pass


class _FakeSignal:
    def connect(self, _handler: object) -> None:
        pass

    def disconnect(self, _handler: object) -> None:
        pass


def _sample_settings() -> AppSettings:
    return AppSettings(
        grid_columns=2,
        grid_rows=1,
        repository_slots=["octocat/Hello-World"],
        card_display=CardDisplaySettings(),
    )


@unittest.skipUnless(
    sys.platform == "win32" or sys.platform.startswith("linux"),
    "WebEngine integration test требует GUI",
)
class WebBridgeIntegrationTests(unittest.TestCase):
    """Статичная страница + auth:status / repos:init через runJavaScript."""

    @classmethod
    def setUpClass(cls) -> None:
        from PySide6.QtWidgets import QApplication

        cls._app = QApplication.instance() or QApplication(sys.argv)

    def test_delivers_auth_and_repos_init(self) -> None:
        from PySide6.QtCore import QEventLoop, QTimer, QUrl
        from PySide6.QtWebEngineWidgets import QWebEngineView

        from github_wallpaper.wallpaper.bridge import BridgeChannelHost, install_bridge_scripts

        view = QWebEngineView()
        host = BridgeChannelHost(lambda _msg: None)
        page = view.page()
        install_bridge_scripts(page, host)

        loop = QEventLoop()
        messages: list[dict] = []

        def finish() -> None:
            loop.quit()

        def poll_messages(attempt: int = 0) -> None:
            page.runJavaScript(
                "JSON.stringify(window.__bridgeTestMessages || [])",
                lambda raw: _collect_messages(raw, attempt),
            )

        def _collect_messages(raw: str, attempt: int) -> None:
            nonlocal messages
            try:
                parsed = json.loads(raw or "[]")
                if isinstance(parsed, list):
                    messages = parsed
            except json.JSONDecodeError:
                messages = []

            types = {item.get("type") for item in messages if isinstance(item, dict)}
            if {"auth:status", "repos:init"}.issubset(types):
                finish()
                return

            if attempt >= 40:
                finish()
                return

            QTimer.singleShot(100, lambda: poll_messages(attempt + 1))

        test_html = (_ASSETS / "bridge-test.html").resolve().as_uri()
        view.loadFinished.connect(
            lambda ok: (
                deliver_json_to_page(
                    page,
                    serialize_message(
                        {
                            "type": "auth:status",
                            "payload": {
                                "hasToken": False,
                                "rateLimit": 60,
                                "rateLimitRemaining": 60,
                                "message": "test",
                            },
                        }
                    ),
                ),
                deliver_json_to_page(
                    page,
                    serialize_message(
                        {
                            "type": "repos:init",
                            "payload": [None, {"owner": "octocat", "repo": "Hello-World"}],
                            "layout": {"columns": 2, "rows": 1},
                            "display": CardDisplaySettings().to_bridge_payload(),
                        }
                    ),
                ),
                QTimer.singleShot(200, lambda: poll_messages(0)),
            )
            if ok
            else finish()
        )
        view.load(QUrl(test_html))
        QTimer.singleShot(10000, finish)
        loop.exec()

        types = {item.get("type") for item in messages if isinstance(item, dict)}
        self.assertIn("auth:status", types, messages)
        self.assertIn("repos:init", types, messages)


if __name__ == "__main__":
    unittest.main()

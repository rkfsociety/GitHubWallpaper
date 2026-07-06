"""Локальный HTTP-сервер для загрузки UI обоев (аналог virtual host app.local)."""

from __future__ import annotations

import threading
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class _QuietHandler(SimpleHTTPRequestHandler):
    """HTTP-обработчик без лишнего вывода в консоль."""

    def log_message(self, format: str, *args: object) -> None:
        pass


class LocalWallpaperServer:
    """Раздаёт каталог wwwroot/wallpaper по http://127.0.0.1:<port>/."""

    def __init__(self, root: Path) -> None:
        self._root = root.resolve()
        self._server: ThreadingHTTPServer | None = None
        self._thread: threading.Thread | None = None
        self.port = 0

    @property
    def url(self) -> str:
        if not self.port:
            raise RuntimeError("Сервер обоев не запущен.")
        return f"http://127.0.0.1:{self.port}/index.html"

    def start(self) -> None:
        if self._server is not None:
            return

        handler = partial(_QuietHandler, directory=str(self._root))
        self._server = ThreadingHTTPServer(("127.0.0.1", 0), handler)
        self.port = self._server.server_address[1]

        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        if self._server is None:
            return

        self._server.shutdown()
        self._server.server_close()
        self._server = None
        self._thread = None
        self.port = 0

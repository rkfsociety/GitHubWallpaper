"""Окно настроек Qt (паритет с SettingsForm.cs)."""

from __future__ import annotations

import asyncio
import json
import logging
from typing import TYPE_CHECKING

from PySide6.QtCore import QThread, QTimer, Signal
from PySide6.QtWidgets import (
    QCheckBox,
    QComboBox,
    QDialog,
    QFormLayout,
    QGridLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QMessageBox,
    QPushButton,
    QRadioButton,
    QVBoxLayout,
    QWidget,
)

from github_wallpaper import autostart
from github_wallpaper.desktop.open_url import open_url
from github_wallpaper.desktop.screen_helper import ScreenHelper
from github_wallpaper.github.credential_store import (
    GitHubOAuthClientSecretCredentialStore,
    GitHubPatCredentialStore,
)
from github_wallpaper.github.gh_cli_auth import get_status
from github_wallpaper.github.oauth import defaults as oauth_defaults
from github_wallpaper.github.oauth.exceptions import GitHubOAuthException
from github_wallpaper.github.oauth.service import GitHubOAuthService
from github_wallpaper.github.poll_intervals import PollIntervalPreset
from github_wallpaper.repo_url_parser import try_parse
from github_wallpaper.settings.grid_layout_editor import GridLayoutEditor
from github_wallpaper.settings.settings_card import SettingsCard
from github_wallpaper.settings.settings_theme import (
    apply_settings_theme,
    style_accent_button,
    style_ghost_button,
    style_muted_label,
    style_outline_button,
)
from github_wallpaper.settings_store import AppSettings, CardDisplaySettings, SettingsStore

if TYPE_CHECKING:
    from github_wallpaper.desktop.auto_pause_monitor import AutoPauseMonitor
    from github_wallpaper.github.github_session import GitHubSession
    from github_wallpaper.github.repo_poller import RepoPoller
    from github_wallpaper.wallpaper.bridge import WallpaperBridge
    from github_wallpaper.wallpaper.controller import WallpaperController

_logger = logging.getLogger(__name__)


class _AsyncWorker(QThread):
    succeeded = Signal(object)
    failed = Signal(str)

    def __init__(self, coroutine_factory) -> None:
        super().__init__()
        self._coroutine_factory = coroutine_factory

    def run(self) -> None:
        try:
            result = asyncio.run(self._coroutine_factory())
            self.succeeded.emit(result)
        except Exception as ex:
            self.failed.emit(str(ex))


class SettingsDialog(QDialog):
    """Настройки: репозитории, сетка, OAuth/PAT, экран, автозапуск."""

    def __init__(
        self,
        *,
        github_session: GitHubSession,
        settings_store: SettingsStore,
        repo_poller: RepoPoller,
        auto_pause_monitor: AutoPauseMonitor,
        wallpaper_controller: WallpaperController,
        bridge: WallpaperBridge,
        parent: QWidget | None = None,
    ) -> None:
        super().__init__(parent)
        self._github_session = github_session
        self._settings_store = settings_store
        self._repo_poller = repo_poller
        self._auto_pause_monitor = auto_pause_monitor
        self._wallpaper_controller = wallpaper_controller
        self._bridge = bridge
        self._suppress_behavior_events = False
        self._suppress_card_display_events = False
        self._async_worker: _AsyncWorker | None = None

        self.setWindowTitle("GitHub Wallpaper — Настройки")
        self.setMinimumSize(920, 580)
        self._build_ui()
        self._load_all()
        self.finished.connect(self._save_window_position)

    def _build_ui(self) -> None:
        apply_settings_theme(self)

        root = QVBoxLayout(self)
        root.setContentsMargins(20, 18, 20, 18)
        root.setSpacing(14)

        root.addWidget(self._build_auth_card())
        root.addWidget(self._build_workspace_card(), stretch=1)
        root.addWidget(self._build_startup_card())

    def _build_auth_card(self) -> SettingsCard:
        card = SettingsCard("Авторизация GitHub")
        self._auth_card = card
        layout = QVBoxLayout(card.body)
        layout.setSpacing(8)

        self._auth_compact = QWidget()
        compact_row = QHBoxLayout(self._auth_compact)
        compact_row.setContentsMargins(0, 0, 0, 0)
        self._auth_user_label = QLabel("")
        self._auth_user_label.setWordWrap(True)
        compact_row.addWidget(self._auth_user_label, stretch=1)
        logout_btn = QPushButton("Выйти")
        style_outline_button(logout_btn)
        logout_btn.clicked.connect(self._on_logout)
        compact_row.addWidget(logout_btn)
        layout.addWidget(self._auth_compact)

        self._auth_details = QWidget()
        details = QVBoxLayout(self._auth_details)
        details.setContentsMargins(0, 0, 0, 0)
        details.setSpacing(8)

        oauth_actions = QHBoxLayout()
        sign_in_btn = QPushButton("Войти через GitHub")
        style_accent_button(sign_in_btn)
        sign_in_btn.clicked.connect(lambda: self._run_oauth(device_only=False))
        oauth_actions.addWidget(sign_in_btn)

        device_btn = QPushButton("Код устройства")
        style_ghost_button(device_btn)
        device_btn.clicked.connect(lambda: self._run_oauth(device_only=True))
        oauth_actions.addWidget(device_btn)

        gh_login_btn = QPushButton("GitHub CLI")
        style_ghost_button(gh_login_btn)
        gh_login_btn.clicked.connect(self._on_gh_login)
        oauth_actions.addWidget(gh_login_btn)

        gh_import_btn = QPushButton("Импорт из gh")
        style_ghost_button(gh_import_btn)
        gh_import_btn.clicked.connect(self._on_gh_import)
        oauth_actions.addWidget(gh_import_btn)
        oauth_actions.addStretch(1)
        details.addLayout(oauth_actions)

        oauth_fields = QHBoxLayout()
        oauth_id_col = QVBoxLayout()
        oauth_id_col.addWidget(QLabel("OAuth Client ID"))
        self._oauth_client_id_input = QLineEdit()
        self._oauth_client_id_input.setPlaceholderText("Developer settings")
        self._oauth_client_id_input.editingFinished.connect(self._save_oauth_client_id)
        oauth_id_col.addWidget(self._oauth_client_id_input)

        oauth_secret_col = QVBoxLayout()
        oauth_secret_col.addWidget(QLabel("Client Secret"))
        self._oauth_client_secret_input = QLineEdit()
        self._oauth_client_secret_input.setEchoMode(QLineEdit.EchoMode.Password)
        self._oauth_client_secret_input.setPlaceholderText("необязательно")
        self._oauth_client_secret_input.editingFinished.connect(self._save_oauth_client_secret)
        oauth_secret_col.addWidget(self._oauth_client_secret_input)

        oauth_fields.addLayout(oauth_id_col, stretch=1)
        oauth_fields.addLayout(oauth_secret_col, stretch=1)
        details.addLayout(oauth_fields)

        create_app_btn = QPushButton("Создать OAuth App")
        style_ghost_button(create_app_btn)
        create_app_btn.clicked.connect(
            lambda: self._open_external_url(oauth_defaults.REGISTRATION_URL)
        )
        details.addWidget(create_app_btn)

        token_row = QHBoxLayout()
        self._token_input = QLineEdit()
        self._token_input.setEchoMode(QLineEdit.EchoMode.Password)
        self._token_input.setPlaceholderText("Personal Access Token")
        token_row.addWidget(self._token_input, stretch=1)

        save_btn = QPushButton("Сохранить")
        style_accent_button(save_btn)
        save_btn.clicked.connect(self._on_save_token)
        token_row.addWidget(save_btn)

        verify_btn = QPushButton("Проверить")
        style_ghost_button(verify_btn)
        verify_btn.clicked.connect(self._on_verify_token)
        token_row.addWidget(verify_btn)

        clear_btn = QPushButton("Очистить")
        style_ghost_button(clear_btn)
        clear_btn.clicked.connect(self._on_clear_token)
        token_row.addWidget(clear_btn)
        details.addLayout(token_row)

        self._token_status_label = QLabel("")
        self._token_status_label.setWordWrap(True)
        style_muted_label(self._token_status_label)
        details.addWidget(self._token_status_label)

        self._gh_status_label = QLabel("")
        self._gh_status_label.setWordWrap(True)
        style_muted_label(self._gh_status_label)
        details.addWidget(self._gh_status_label)

        layout.addWidget(self._auth_details)
        return card

    def _build_workspace_card(self) -> SettingsCard:
        card = SettingsCard("Сетка обоев")
        row = QHBoxLayout(card.body)
        row.setSpacing(16)

        repos_panel = QVBoxLayout()
        repos_panel.setSpacing(8)

        self._grid_editor = GridLayoutEditor()
        self._grid_apply_timer = QTimer(self)
        self._grid_apply_timer.setSingleShot(True)
        self._grid_apply_timer.setInterval(250)
        self._grid_apply_timer.timeout.connect(self._apply_grid_layout)
        self._grid_editor.layout_changed.connect(self._schedule_grid_layout_apply)
        repos_panel.addWidget(self._grid_editor)

        repo_row = QHBoxLayout()
        self._repo_input = QLineEdit()
        self._repo_input.setPlaceholderText("owner/repo или URL GitHub")
        self._repo_input.returnPressed.connect(self._on_add_repo)
        repo_row.addWidget(self._repo_input, stretch=1)

        add_btn = QPushButton("Добавить")
        style_accent_button(add_btn)
        add_btn.clicked.connect(self._on_add_repo)
        repo_row.addWidget(add_btn)

        remove_btn = QPushButton("Удалить")
        style_ghost_button(remove_btn)
        remove_btn.clicked.connect(self._on_remove_repo)
        repo_row.addWidget(remove_btn)
        repos_panel.addLayout(repo_row)

        row.addLayout(repos_panel, stretch=1)
        row.addWidget(self._build_sidebar(), stretch=0)
        return card

    def _build_sidebar(self) -> QWidget:
        sidebar = QWidget()
        sidebar.setFixedWidth(300)
        layout = QVBoxLayout(sidebar)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(12)

        layout.addWidget(self._build_display_group())
        layout.addWidget(self._build_monitor_group())
        layout.addWidget(self._build_poll_group())
        layout.addStretch(1)
        return sidebar

    def _build_display_group(self) -> SettingsCard:
        card = SettingsCard("Содержимое карточек")
        grid = QGridLayout(card.body)
        grid.setHorizontalSpacing(12)
        grid.setVerticalSpacing(4)

        self._display_toggles: dict[str, QCheckBox] = {}
        labels = {
            "show_description": "Описание",
            "show_stats": "Статистика",
            "show_ci": "CI/CD",
            "show_release": "Релизы",
            "show_heatmap": "Heatmap",
            "show_feed": "Лента",
            "show_pull_requests": "Pull requests",
            "show_issues": "Issues",
            "show_commits": "Коммиты",
        }
        for index, (field_name, label) in enumerate(labels.items()):
            checkbox = QCheckBox(label)
            checkbox.toggled.connect(self._on_card_display_changed)
            self._display_toggles[field_name] = checkbox
            grid.addWidget(checkbox, index // 2, index % 2)
        return card

    def _build_monitor_group(self) -> SettingsCard:
        card = SettingsCard("Экран")
        form = QFormLayout(card.body)
        form.setContentsMargins(0, 0, 0, 0)
        self._monitor_combo = QComboBox()
        self._monitor_combo.currentIndexChanged.connect(self._on_behavior_changed)
        form.addRow("Монитор:", self._monitor_combo)
        return card

    def _build_poll_group(self) -> SettingsCard:
        card = SettingsCard("Интервал обновления")
        layout = QVBoxLayout(card.body)
        layout.setSpacing(4)

        self._economy_radio = QRadioButton("Экономный — 20 мин")
        self._normal_radio = QRadioButton("Нормальный — 10 мин")
        self._frequent_radio = QRadioButton("Частый — 1 мин")
        self._normal_radio.setChecked(True)

        for radio in (self._economy_radio, self._normal_radio, self._frequent_radio):
            radio.toggled.connect(self._on_behavior_changed)
            layout.addWidget(radio)
        return card

    def _build_startup_card(self) -> SettingsCard:
        card = SettingsCard("Запуск и поведение")
        grid = QGridLayout(card.body)
        grid.setHorizontalSpacing(16)
        grid.setVerticalSpacing(4)

        self._auto_start_check = QCheckBox("Запускать при входе в систему")
        self._pause_fullscreen_check = QCheckBox("Пауза при полноэкранном приложении")
        self._pause_battery_check = QCheckBox("Пауза при работе от батареи")
        self._auto_updates_check = QCheckBox("Автоматически проверять обновления")

        checks = (
            self._auto_start_check,
            self._pause_fullscreen_check,
            self._pause_battery_check,
            self._auto_updates_check,
        )
        for index, checkbox in enumerate(checks):
            checkbox.toggled.connect(self._on_behavior_changed)
            grid.addWidget(checkbox, index // 2, index % 2)
        return card

    def _load_all(self) -> None:
        settings = self._settings_store.load()
        self._populate_monitors(settings.display_device_name)
        self._load_behavior_settings(settings)
        self._load_card_display(settings.card_display)
        self._load_oauth_fields(settings)
        self._grid_editor.set_layout(
            settings.grid_columns,
            settings.grid_rows,
            settings.repository_slots,
        )
        self._update_token_status()
        self._update_gh_status()
        self._restore_window_position(settings)

    def _populate_monitors(self, selected_device_name: str) -> None:
        self._monitor_combo.clear()
        screens = ScreenHelper.all_screens()
        selected_index = 0
        resolved = ScreenHelper.resolve(selected_device_name)

        for index, screen in enumerate(screens):
            self._monitor_combo.addItem(ScreenHelper.format_label(screen, index), screen.name())
            if screen.name().lower() == resolved.name().lower():
                selected_index = index

        if self._monitor_combo.count() > 0:
            self._monitor_combo.setCurrentIndex(selected_index)

    def _load_behavior_settings(self, settings: AppSettings) -> None:
        self._suppress_behavior_events = True

        if settings.poll_interval_preset is PollIntervalPreset.ECONOMY:
            self._economy_radio.setChecked(True)
        elif settings.poll_interval_preset is PollIntervalPreset.FREQUENT:
            self._frequent_radio.setChecked(True)
        else:
            self._normal_radio.setChecked(True)

        self._auto_start_check.setChecked(settings.auto_start)
        self._pause_fullscreen_check.setChecked(settings.pause_on_fullscreen)
        self._pause_battery_check.setChecked(settings.pause_on_battery)
        self._auto_updates_check.setChecked(settings.auto_check_for_updates)

        self._suppress_behavior_events = False

    def _load_card_display(self, display: CardDisplaySettings) -> None:
        self._suppress_card_display_events = True
        for field_name, checkbox in self._display_toggles.items():
            checkbox.setChecked(getattr(display, field_name))
        self._suppress_card_display_events = False

    def _load_oauth_fields(self, settings: AppSettings) -> None:
        self._oauth_client_id_input.setText(settings.github_oauth_client_id)
        secret = GitHubOAuthClientSecretCredentialStore.read()
        self._oauth_client_secret_input.setText(secret or "")

    def _restore_window_position(self, settings: AppSettings) -> None:
        if settings.settings_window_left is not None and settings.settings_window_top is not None:
            self.move(settings.settings_window_left, settings.settings_window_top)
        else:
            self.resize(940, 600)

    def _save_window_position(self) -> None:
        try:
            settings = self._settings_store.load()
            settings.settings_window_left = self.x()
            settings.settings_window_top = self.y()
            self._settings_store.save(settings)
        except Exception:
            _logger.debug("Не удалось сохранить позицию окна настроек", exc_info=True)

    def _selected_poll_preset(self) -> PollIntervalPreset:
        if self._economy_radio.isChecked():
            return PollIntervalPreset.ECONOMY
        if self._frequent_radio.isChecked():
            return PollIntervalPreset.FREQUENT
        return PollIntervalPreset.NORMAL

    def _on_behavior_changed(self) -> None:
        if self._suppress_behavior_events:
            return
        self._save_behavior_settings()

    def _save_behavior_settings(self) -> None:
        try:
            settings = self._settings_store.load()
            settings.poll_interval_preset = self._selected_poll_preset()
            settings.auto_start = self._auto_start_check.isChecked()
            settings.pause_on_fullscreen = self._pause_fullscreen_check.isChecked()
            settings.pause_on_battery = self._pause_battery_check.isChecked()
            settings.auto_check_for_updates = self._auto_updates_check.isChecked()

            device_name = self._monitor_combo.currentData()
            settings.display_device_name = str(device_name or "")

            self._settings_store.save(settings)
            self._repo_poller.configure_poll_intervals(settings.poll_interval_preset)
            autostart.set_enabled(settings.auto_start)
            self._auto_pause_monitor.configure(settings)
            self._wallpaper_controller.configure_display(settings.display_device_name)
        except Exception as ex:
            QMessageBox.critical(self, self.windowTitle(), f"Не удалось сохранить настройки:\n{ex}")

    def _on_card_display_changed(self) -> None:
        if self._suppress_card_display_events:
            return
        try:
            settings = self._settings_store.load()
            settings.card_display = self._current_card_display()
            self._settings_store.save(settings)
            self._repo_poller.configure_card_display(settings.card_display)
            self._bridge.push_display_settings()
        except Exception as ex:
            QMessageBox.critical(self, self.windowTitle(), f"Не удалось сохранить отображение:\n{ex}")

    def _current_card_display(self) -> CardDisplaySettings:
        return CardDisplaySettings(
            **{field: checkbox.isChecked() for field, checkbox in self._display_toggles.items()}
        )

    def _save_oauth_client_id(self) -> None:
        settings = self._settings_store.load()
        settings.github_oauth_client_id = self._oauth_client_id_input.text().strip()
        self._settings_store.save(settings)

    def _save_oauth_client_secret(self) -> None:
        secret = self._oauth_client_secret_input.text().strip()
        if not secret:
            GitHubOAuthClientSecretCredentialStore.delete()
            return
        try:
            GitHubOAuthClientSecretCredentialStore.save(secret)
        except ValueError as ex:
            QMessageBox.warning(self, self.windowTitle(), str(ex))

    def _on_add_repo(self) -> None:
        text = self._repo_input.text().strip()
        if not text:
            QMessageBox.warning(self, self.windowTitle(), "Введите owner/repo или URL репозитория.")
            return

        reference = try_parse(text)
        if reference is None:
            QMessageBox.warning(
                self,
                self.windowTitle(),
                "Неверный формат. Используйте owner/repo или https://github.com/owner/repo.",
            )
            return

        if self._grid_editor.contains_repository(reference.slug):
            QMessageBox.information(self, self.windowTitle(), f"Репозиторий {reference.slug} уже в сетке.")
            return

        if not self._grid_editor.try_add_repository(reference.slug):
            QMessageBox.warning(
                self,
                self.windowTitle(),
                "Сетка заполнена. Увеличьте размер или удалите репозиторий.",
            )
            return

        self._repo_input.clear()
        self._apply_grid_layout()

    def _schedule_grid_layout_apply(self) -> None:
        self._grid_apply_timer.start()

    def _open_external_url(self, url: str) -> None:
        if open_url(url):
            return
        QMessageBox.information(
            self,
            self.windowTitle(),
            "Браузер не открылся (часто при запуске от root).\n"
            f"Ссылка скопирована в буфер обмена:\n{url}",
        )

    def _on_remove_repo(self) -> None:
        if self._grid_editor.occupied_slot_count <= 1:
            QMessageBox.warning(self, self.windowTitle(), "В сетке должен остаться хотя бы один репозиторий.")
            return

        if not self._grid_editor.try_remove_selected_repository():
            QMessageBox.warning(self, self.windowTitle(), "Выберите ячейку с репозиторием для удаления.")
            return

        self._apply_grid_layout()

    def _apply_grid_layout(self) -> None:
        try:
            self._settings_store.save_grid_layout(
                self._grid_editor.grid_columns,
                self._grid_editor.grid_rows,
                self._grid_editor.get_slots(),
            )
            repositories = self._settings_store.load_repositories()
            self._repo_poller.start(repositories)
            self._bridge.push_layout()
            self._bridge.push_repo_list()
        except Exception as ex:
            QMessageBox.critical(self, self.windowTitle(), f"Не удалось сохранить сетку:\n{ex}")

    def _update_token_status(self) -> None:
        if self._github_session.has_stored_token:
            self._token_status_label.setText(
                "Токен в Credential Manager. «Войти через GitHub» заменит текущий."
            )
            self._auth_user_label.setText("Статус: токен сохранён")
        elif self._github_session.uses_gh_token:
            self._token_status_label.setText(
                "Токен из GitHub CLI (gh). «Импорт из gh» — сохранить в keyring."
            )
            self._auth_user_label.setText("Статус: токен из gh")
        else:
            self._token_status_label.setText(
                "Войдите через GitHub CLI, браузер или вставьте PAT. Без токена — 60 запросов/час."
            )
            self._auth_user_label.setText("Статус: токен не задан")
        self._update_auth_view()

    def _update_auth_view(self) -> None:
        signed_in = self._github_session.has_stored_token or self._github_session.uses_gh_token
        self._auth_details.setVisible(not signed_in)
        self._auth_compact.setVisible(signed_in)
        if hasattr(self, "_auth_card"):
            self._auth_card.set_title_visible(not signed_in)

    def _update_gh_status(self) -> None:
        status = get_status()
        if not status.available:
            self._gh_status_label.setText(status.message or "GitHub CLI (gh) недоступен.")
            return
        if not status.logged_in:
            self._gh_status_label.setText(
                status.message or "В gh нет входа. Установите gh и выполните вход."
            )
            return

        username = f" @{status.username}" if status.username else ""
        scopes = ", ".join(status.scopes) if status.scopes else "не указаны"
        self._gh_status_label.setText(f"GitHub CLI:{username} — scopes: {scopes}")

    def _on_save_token(self) -> None:
        token = self._token_input.text().strip()
        if not token:
            QMessageBox.warning(self, self.windowTitle(), "Введите токен для сохранения.")
            return
        try:
            self._github_session.save_token(token)
            self._token_input.clear()
            self._update_token_status()
            QMessageBox.information(self, self.windowTitle(), "Токен сохранён.")
        except Exception as ex:
            QMessageBox.critical(self, self.windowTitle(), f"Не удалось сохранить токен:\n{ex}")

    def _on_clear_token(self) -> None:
        try:
            self._github_session.clear_token()
            self._github_session.reload_token_from_gh()
            self._token_input.clear()
            self._update_token_status()
            self._update_gh_status()
            QMessageBox.information(self, self.windowTitle(), "Токен удалён из хранилища.")
        except Exception as ex:
            QMessageBox.critical(self, self.windowTitle(), f"Не удалось удалить токен:\n{ex}")

    def _on_verify_token(self) -> None:
        token = self._token_input.text().strip()
        if token:
            try:
                self._github_session.save_token(token)
                self._token_input.clear()
                self._update_token_status()
            except Exception as ex:
                QMessageBox.critical(self, self.windowTitle(), f"Не удалось сохранить токен:\n{ex}")
                return
        elif not self._github_session.has_token:
            QMessageBox.warning(self, self.windowTitle(), "Сначала введите и сохраните токен.")
            return
        elif self._github_session.has_stored_token:
            self._github_session.reload_token_from_store()
        else:
            self._github_session.reload_token_from_gh()

        async def _verify():
            return await self._github_session.client.get_authenticated_user()

        self._run_async(_verify, self._on_verify_succeeded, self._on_verify_failed)

    def _on_verify_succeeded(self, result) -> None:
        self._update_token_status()
        login = _try_read_github_login(result.body)
        if login:
            self._auth_user_label.setText(f"Вошли как @{login}")
        QMessageBox.information(self, self.windowTitle(), "Токен действителен.")

    def _on_verify_failed(self, message: str) -> None:
        QMessageBox.critical(self, self.windowTitle(), f"Не удалось проверить токен:\n{message}")

    def _on_logout(self) -> None:
        self._on_clear_token()

    def _on_gh_login(self) -> None:
        async def _login() -> None:
            await asyncio.to_thread(self._github_session.login_with_gh)

        def _on_success(_result) -> None:
            self._update_token_status()
            self._update_gh_status()
            QMessageBox.information(self, self.windowTitle(), "Вход через GitHub CLI выполнен.")

        def _on_failed(message: str) -> None:
            QMessageBox.critical(
                self,
                self.windowTitle(),
                f"Не удалось войти через GitHub CLI:\n{message}",
            )

        self._run_async(_login, _on_success, _on_failed)

    def _on_gh_import(self) -> None:
        async def _import() -> None:
            await asyncio.to_thread(self._github_session.import_token_from_gh)

        def _on_success(_result) -> None:
            self._update_token_status()
            self._update_gh_status()
            QMessageBox.information(self, self.windowTitle(), "Токен импортирован из GitHub CLI.")

        def _on_failed(message: str) -> None:
            if isinstance(message, str) and "GhCliAuthError" in message:
                message = message.split(":", 1)[-1].strip()
            QMessageBox.critical(
                self,
                self.windowTitle(),
                f"Не удалось импортировать токен из gh:\n{message}",
            )

        self._run_async(_import, _on_success, _on_failed)

    def _run_oauth(self, *, device_only: bool) -> None:
        self._save_oauth_client_id()
        self._save_oauth_client_secret()

        settings = self._settings_store.load()
        stored_secret = GitHubOAuthClientSecretCredentialStore.read()
        oauth = GitHubOAuthService(
            settings_client_id=settings.github_oauth_client_id,
            stored_client_secret=stored_secret,
            open_url=self._oauth_open_url,
        )

        async def _sign_in():
            if device_only:
                return await oauth.sign_in_with_device_flow()
            return await oauth.sign_in()

        def _on_success(result) -> None:
            self._github_session.save_token(result.access_token)
            self._update_token_status()
            if result.user_code:
                QMessageBox.information(
                    self,
                    self.windowTitle(),
                    f"Вход выполнен (код устройства: {result.user_code}).",
                )
            else:
                QMessageBox.information(self, self.windowTitle(), "Вход через GitHub выполнен.")

        self._run_async(_sign_in, _on_success, lambda msg: QMessageBox.critical(self, self.windowTitle(), msg))

    def _oauth_open_url(self, url: str) -> None:
        if open_url(url):
            return
        raise GitHubOAuthException(
            "Не удалось открыть браузер (часто при запуске от root). "
            "Ссылка скопирована в буфер обмена — используйте вход по коду устройства."
        )

    def _run_async(self, coroutine_factory, on_success, on_failed) -> None:
        if self._async_worker is not None and self._async_worker.isRunning():
            QMessageBox.information(self, self.windowTitle(), "Подождите завершения текущей операции.")
            return

        worker = _AsyncWorker(coroutine_factory)
        worker.succeeded.connect(on_success)
        worker.failed.connect(on_failed)
        worker.finished.connect(lambda: setattr(self, "_async_worker", None))
        self._async_worker = worker
        worker.start()


def _try_read_github_login(body: str) -> str | None:
    if not body:
        return None
    try:
        payload = json.loads(body)
    except json.JSONDecodeError:
        return None
    if isinstance(payload, dict):
        login = payload.get("login")
        if isinstance(login, str) and login:
            return login
    return None

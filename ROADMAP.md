# Дорожная карта GitHubWallpaper

Динамические обои с отслеживанием активности GitHub-репозиториев.

**Стек v1.0 (Windows):** C# / .NET 8 · WebView2 · WorkerW · GitHub REST API  
**Стек v2.0 (кроссплатформа):** Python 3.11+ · PySide6 · Qt WebEngine · GitHub REST API

---

## Обзор этапов

| Этап | Название | Статус | Цель |
|------|----------|--------|------|
| 1 | [Каркас движка](#этап-1--каркас-mvp-движка) | ✅ Готов | Обои на рабочем столе + трей |
| 2 | [GitHub: один репо](#этап-2--github--один-репозиторий) | ✅ Готов | Первая живая карточка репозитория |
| 3 | [Мульти-репо и настройки](#этап-3--мульти-репо-и-настройки) | ✅ Готов | Список репо, токен, автозапуск |
| 4 | [Полная активность](#этап-4--полная-активность) | ✅ Готов | PR, issues, CI, heatmap, лента |
| 5 | [Полировка и релиз](#этап-5--полировка-и-релиз) | ✅ Готов | Стабильный exe, CI/CD, документация |
| 6 | [Python + Qt (v2.0)](#этап-6--python--qt-v20-кроссплатформа) | 🔄 В работе | Windows + Linux, общий UI и логика |
| — | [Фаза 2 (будущее)](#фаза-2--будущее) | 💡 Идеи | Расширения после v2.0 |

**Текущее состояние:** v1.0 (C#) — [Release `latest`](https://github.com/rkfsociety/GitHubWallpaper/releases/latest); push в `main` → CI собирает portable exe. v2.0 (Python/PySide6) — разработка в ветке `python-qt` (заменяет заготовку `python-gtk-linux`).

---

## Этап 1 — Каркас (MVP движка)

**Цель:** приложение запускается, показывает HTML-обои за иконками рабочего стола, управляется из трея.

### Задачи

- [x] Создать solution: `GitHubWallpaper.csproj`, `app.manifest` (PerMonitorV2 DPI)
- [x] Подключить NuGet: `Microsoft.Web.WebView2`
- [x] Реализовать `DesktopHost` — техника WorkerW (Progman → `0x052C`)
- [x] Реализовать `WallpaperSurface` — borderless WebView2 в WorkerW
- [x] Реализовать `WallpaperController` — apply / pause / resume
- [x] Реализовать `TrayService` — иконка в трее: Настройки / Пауза / Выход
- [x] Загрузить статичную страницу `wwwroot/wallpaper/index.html` через virtual host (`app.local`)

### Критерии готовности

- [x] Exe запускается без установки .NET (self-contained, профиль `win-x64.pubxml`)
- [x] HTML-страница видна **позади** иконок рабочего стола
- [x] Пауза из трея останавливает рендер
- [x] Выход из трея корректно завершает процесс

---

## Этап 2 — GitHub + один репозиторий

**Цель:** на обоях отображается карточка одного репозитория с базовыми метриками.

### Задачи

- [x] `RepoUrlParser` — разбор `owner/repo` и `https://github.com/owner/repo`
- [x] `GitHubApiClient` — HTTP-клиент с заголовком `Authorization`
- [x] Хранение PAT в Windows Credential Manager (не в JSON)
- [x] `RateLimitGuard` — отслеживание `X-RateLimit-*`, backoff при 403
- [x] `RepoPoller` — опрос мета-данных и коммитов
- [x] `Bridge` — push JSON в WebView2 через `PostWebMessageAsJson`
- [x] Виджет `repo-card` — stars, forks, последние 5 коммитов
- [x] `auth:status` и `repos:init` — статус токена и список репо на старте
- [x] Предупреждение в трее и баннер на обоях при отсутствии PAT

### GitHub API (этап 2)

| Данные | Endpoint | Интервал |
|--------|----------|----------|
| Мета | `GET /repos/{owner}/{repo}` | 5 мин |
| Коммиты | `GET /repos/{owner}/{repo}/commits?per_page=5` | 2 мин |

### Критерии готовности

- [x] Добавлен репо `microsoft/vscode` → через 2–3 мин видны stars и коммиты
- [x] Без токена — предупреждение, лимит 60 req/h
- [x] С токеном — `GET /user` проходит успешно

---

## Этап 3 — Мульти-репо и настройки

**Цель:** полноценное окно настроек, несколько репозиториев, сохранение конфигурации.

### Задачи

- [x] `SettingsForm` (WinForms) — окно настроек (базовое)
- [x] Список репозиториев: добавить / удалить / переупорядочить
- [x] Поле GitHub Token + кнопки «Сохранить» / «Проверить» / «Очистить»
- [x] `SettingsStore` — `%APPDATA%\GitHubWallpaper\settings.json`
- [x] Пресеты интервалов опроса: экономный / нормальный / частый
- [x] Автозапуск (registry Run key)
- [x] Пауза при полноэкранных приложениях (`GetForegroundWindow`)
- [x] Пауза на батарее

### Критерии готовности

- [x] 3+ репозитория отображаются адаптивной сеткой
- [x] Настройки сохраняются между перезапусками
- [x] Автозапуск работает (в трей)

---

## Этап 4 — Полная активность

**Цель:** все запрошенные виджеты активности GitHub на обоях.

### Задачи

- [x] PR — `GET /repos/{owner}/{repo}/pulls?state=open`
- [x] Issues — `GET /repos/{owner}/{repo}/issues?state=open` (без PR)
- [x] Релизы — `GET /repos/{owner}/{repo}/releases?per_page=5`
- [x] CI/CD — `GET /repos/{owner}/{repo}/actions/runs?per_page=1`
- [x] Heatmap — `GET /repos/{owner}/{repo}/stats/commit_activity` (retry при 202)
- [x] `ActivityAggregator` — единая лента из events + дельт polling
- [x] Виджеты: `heatmap.js`, `feed.js`, `ci-badge.js`
- [x] ETag / `If-None-Match` для кэширования ответов API
- [x] Клик по карточке → открыть репо в браузере

### GitHub API (этап 4)

| Виджет | Endpoint | Интервал |
|--------|----------|----------|
| PR | `GET .../pulls?state=open&per_page=10` | 3 мин |
| Issues | `GET .../issues?state=open&per_page=10` | 3 мин |
| Релизы | `GET .../releases?per_page=5` | 10 мин |
| CI/CD | `GET .../actions/runs?per_page=1` | 3 мин |
| Heatmap | `GET .../stats/commit_activity` | 1 час |
| Лента | `GET .../events?per_page=30` | 2 мин |

### Критерии готовности

- [x] Push в тестовый репо → событие появляется в ленте
- [x] Heatmap рисуется за 52 недели
- [x] CI badge показывает статус последнего workflow run
- [x] Анимация при появлении новых событий

---

## Этап 5 — Полировка и релиз

**Цель:** стабильный релиз v1.0, готовый к ежедневному использованию.

### Задачи

- [x] Обработка ошибок: 404, private repo, сетевые сбои
- [x] Проверка WebView2 runtime при старте + ссылка на bootstrapper
- [x] GitHub Actions `ci.yml` — сборка на PR, Release `latest` на push в `main`
- [x] OAuth-вход через github.com (Authorization Code + PKCE, Device Flow)
- [x] Автообновление portable exe из Release `latest`
- [x] Первый запуск: копирование в AppData, ярлыки на рабочем столе и в «Пуск»
- [x] Выбор монитора и корректное позиционирование на нескольких экранах
- [x] README: установка, создание PAT, скриншоты, FAQ
- [x] Тест на Windows 11 24H2 (сборка и publish на build 10.0.26300)

### Критерии готовности

- [x] Релизы публикуются автоматически при push в `main` (один Release `latest`, пересоздаётся через `--target`)
- [x] Portable exe работает без установки .NET
- [ ] Все 5 сценариев из раздела «Проверка» пройдены (ручная проверка)

---

## Этап 6 — Python + Qt (v2.0, кроссплатформа)

**Цель:** единое приложение на **Windows и Linux** с переиспользованием `wwwroot/wallpaper/` и совместимым `settings.json`. C#-версия (этапы 1–5) остаётся в `src/` до паритета функций v2.0.

**Ветка:** `python-qt`  
**Каталог:** `python/` (`pyproject.toml`, пакет `github_wallpaper`)

### Стратегия

| Слой | Решение |
|------|---------|
| UI обоев | `wwwroot/wallpaper/` без изменений (HTML/CSS/JS) |
| WebView | `QWebEngineView` (Chromium, есть на Win и Linux) |
| Bridge | Тот же JSON-протокол, что в `Bridge.cs` / `app.js` |
| Настройки | Тот же `settings.json` (без PAT) |
| Секреты | `keyring` (Win Credential Manager / Linux Secret Service) |
| Обои | Платформенные бэкенды: WorkerW (Win) · desktop layer (Linux) |
| Трей | `QSystemTrayIcon` |
| Окно настроек | Qt Widgets (PySide6), функциональный паритет с `SettingsForm` |

### Архитектура v2.0

```
QSystemTrayIcon + SettingsDialog (PySide6)
        │
        ▼
WallpaperController ──► DesktopBackend ──► QWebEngineView
   (windows / linux)         │                    ▲
        │                   │                    │
        ▼                   ▼                    │
RepoPoller ──► GitHubClient ──► Bridge (JSON) ──┘
                    │
                    ▼
              keyring (PAT)
```

### 6.1 — Каркас проекта

- [ ] Ветка `python-qt`; `python/pyproject.toml` (PySide6, httpx, keyring, pydantic)
- [ ] Структура пакета: `main.py`, `app.py`, `paths.py`, `single_instance.py`
- [ ] `paths.py`: Windows `%APPDATA%` · Linux XDG (`XDG_CONFIG_HOME`, `XDG_DATA_HOME`, `XDG_CACHE_HOME`)
- [ ] `QApplication` + `QSystemTrayIcon`: меню Настройки / Пауза / Выход
- [ ] Single-instance (named mutex на Win · Unix socket на Linux)
- [ ] Копирование `wwwroot/wallpaper/` в каталог данных при первом запуске (аналог `AppInstaller.EnsureWallpaperAssets`)

### 6.2 — Обои и WebEngine

- [ ] `WallpaperWindow` — borderless `QWebEngineView` на весь выбранный экран
- [ ] Загрузка UI: локальный HTTP-сервер или `qrc://` / `file://` + разрешения WebEngine
- [ ] `DesktopBackend` (абстракция): `apply` / `remove` / `pause` / `resume` / `set_screen`
- [ ] **Windows:** `WindowsDesktopBackend` — WorkerW через `ctypes` (порт логики `DesktopHost.cs`)
- [ ] **Linux:** `LinuxDesktopBackend` — окно типа desktop (X11 `_NET_WM_WINDOW_TYPE_DESKTOP`; Wayland — по DE, MVP на X11)
- [ ] Выбор монитора: `QScreen` / `DisplayDeviceName` в настройках (совместимость с полем из C#)
- [ ] Пауза рендера: `QWebEngineView.setVisible(False)` + снижение polling

### 6.3 — Bridge (C# ↔ JS)

Сообщения **без изменений** в `app.js` (см. `Bridge.cs`):

`pause` · `resume` · `auth:status` · `repos:init` · `repo:metadata` · `repo:commits` · `repo:pulls` · `repo:issues` · `repo:releases` · `repo:ci-run` · `repo:heatmap` · `repo:activity-feed` · `repo:poll-failed` · (JS→host) `open-url` · `page:ready`

- [ ] Shim для `window.chrome.webview` в WebEngine: `QWebChannel` или `runJavaScript` + `QWebEngineScript` injection
- [ ] `bridge.py` — сериализация JSON (camelCase), очередь до `page:ready`
- [ ] Обработка `open-url` → `QDesktopServices.openUrl`
- [ ] Тест: статичная страница + `auth:status` / `repos:init` в консоли

### 6.4 — GitHub и polling

Порт логики из `src/GitHub/` (не копипаста, а эквивалентное поведение):

- [ ] `repo_url_parser.py` — `owner/repo`, URL GitHub
- [ ] `github_client.py` — httpx, `Authorization`, ETag / `If-None-Match`
- [ ] `rate_limit_guard.py` — заголовки `X-RateLimit-*`, backoff при 403
- [ ] `repo_poller.py` — asyncio или `QThread` + таймеры; те же интервалы, что `PollIntervals`
- [ ] `activity_aggregator.py` — лента событий
- [ ] `keyring` store для PAT и OAuth client secret (имена как в C#: `GitHubWallpaper/PersonalAccessToken`)
- [ ] OAuth: Authorization Code + PKCE и Device Flow (порт `GitHubOAuthService`)

### 6.5 — Настройки (Qt)

- [ ] `SettingsStore` — чтение/запись `settings.json` (поля из `AppSettings.cs`, camelCase)
- [ ] `SettingsDialog` — репозитории, сетка, интервалы, экран, OAuth/PAT, видимость блоков карточек
- [ ] Миграция: Qt-приложение читает существующий `%APPDATA%` / `~/.config` файл от C#-версии
- [ ] Автозапуск: registry Run (Win) · `.desktop` в `~/.config/autostart` (Linux)
- [ ] Пауза при полноэкранном приложении и на батарее (платформенные хуки)

### 6.6 — Сборка, CI и релиз

- [ ] `README` раздел v2.0: зависимости (PySide6, Qt WebEngine), запуск из исходников
- [ ] Сборка: PyInstaller / cx_Freeze — `GitHubWallpaper` exe (Win) и AppImage или tarball (Linux)
- [ ] GitHub Actions: job `python-qt` на `windows-latest` и `ubuntu-latest`
- [ ] Release: отдельный тег `v2.0` или pre-release `v2.0-beta` (не ломать `latest` для C# до готовности)
- [ ] Автообновление v2.0 из GitHub Releases (порт `AppUpdateService`)

### Критерии готовности v2.0

- [ ] **Windows:** обои за иконками, трей, настройки, 3+ репо, PAT/OAuth, пауза
- [ ] **Linux (X11):** обои на рабочем столе, трей, настройки, те же виджеты на обоях
- [ ] `settings.json` и PAT переносятся между C# v1 и Python v2 на одной ОС
- [ ] Все сценарии из раздела «Проверка» пройдены на Win и Linux
- [ ] CI зелёный на обеих платформах

### Риски v2.0

| Риск | Митигация |
|------|-----------|
| Qt WebEngine тяжёлый (~150 MB) | PyInstaller one-folder; документировать размер |
| `window.chrome.webview` нет в WebEngine | JS-shim + QWebChannel в `bridge-shim.js` |
| WorkerW на новых сборках Win11 | Порт проверенной логики из `DesktopHost.cs`; fallback overlay |
| Wayland на Linux | MVP на X11; Wayland — отдельный подэтап по DE |
| Два стека в репо (C# + Python) | v1 в `main`/`src`, v2 в `python-qt`/`python` до слияния |
| Дублирование логики GitHub | Единая таблица API в ROADMAP; тесты на паритет ответов bridge |

### Порядок реализации (кратко)

```
6.1 каркас → 6.3 bridge (минимальный) → 6.2 обои (Win) → 6.4 GitHub → 6.5 настройки
     → 6.2 Linux backend → 6.6 CI/релиз
```

---

## Фаза 2 — Будущее

Идеи после v2.0, не входят в текущий scope этапа 6:

- [x] Выбор одного монитора для отображения обоев (настройки → «Экран»)
- [x] Корректное позиционирование обоев на втором и последующих мониторах
- [x] OAuth-вход через github.com (Authorization Code + PKCE и Device Flow)
- [x] Автообновление portable exe из GitHub Release `latest`
- [x] Первый запуск: копирование в AppData и ярлыки
- GraphQL batch-запросы для 10+ репозиториев
- Per-monitor: разные наборы репо на каждом мониторе
- Кастомные темы / пользовательский CSS
- Webhook / real-time обновления
- Звуковые уведомления о важных событиях

---

## Архитектура

```
Tray + SettingsForm (WinForms)
        │
        ▼
WallpaperController ──► DesktopHost (WorkerW) ──► WebView2
        │                                              ▲
        ▼                                              │
RepoPoller ──► GitHubApiClient ──► ActivityAggregator ─┘
                    │                    Bridge (JSON)
                    ▼
              Credential Manager (PAT)
```

---

## Риски

| Риск | Митигация |
|------|-----------|
| Rate limit GitHub (60/h без токена) | PAT в настройках, умные интервалы, ETag |
| `stats/*` возвращает HTTP 202 | Retry 2–5 сек, кэш последнего ответа |
| WebView2 не установлен | Проверка при старте + ссылка на bootstrapper |
| WorkerW на Win11 24H2 | Тестирование; fallback borderless overlay |
| Приватные репо | PAT со scope `repo` |

---

## Проверка (финальный чеклист)

1. Запуск exe → обои появляются за иконками
2. Добавление `microsoft/vscode` → stars, commits, PR видны за 2–3 мин
3. Push в свой тестовый репо → событие в ленте
4. Pause из трея → обои замирают, polling снижается
5. Без токена → предупреждение + ограниченный режим
6. Второй монитор → обои на выбранном экране, не смещаются на основной

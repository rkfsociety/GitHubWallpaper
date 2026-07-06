# Дорожная карта GitHubWallpaper

Динамические обои с отслеживанием активности GitHub-репозиториев.

**Текущий стек:** Python 3.11+ · PySide6 · Qt WebEngine · GitHub REST API · Windows + Linux (X11)  
**Релиз:** [Release `latest`](https://github.com/rkfsociety/GitHubWallpaper/releases/latest) — push в `main` → CI собирает portable-архивы Win/Linux.  
**Архив v1.0:** C# / .NET 8 / WebView2 в `src/` (этапы 1–5, CI больше не собирает).

---

## Обзор этапов

| Этап | Название | Статус | Цель |
|------|----------|--------|------|
| 1–5 | v1.0 (C#, Windows) | ✅ Архив | Каркас, GitHub, настройки, релиз — см. [историю](#этапы-15--v10-архив) |
| 6 | [Python + Qt (v2.0)](#этап-6--python--qt-v20) | 🔄 В работе | Кроссплатформа, паритет функций, Release `latest` |
| — | [Фаза 2 (будущее)](#фаза-2--будущее) | 💡 Идеи | Расширения после v2.0 |

---

## Этапы 1–5 (v1.0, архив)

Реализовано в `src/` (C# / WinForms / WebView2 / WorkerW). Этапы закрыты; исходники сохранены для справки.

| Этап | Содержание |
|------|------------|
| 1 | WorkerW, WebView2, трей, virtual host `app.local` |
| 2 | GitHub API, PAT, bridge, карточка репозитория |
| 3 | Мульти-репо, `settings.json`, автозапуск, пауза |
| 4 | PR, issues, релизы, CI badge, heatmap, лента |
| 5 | OAuth, автообновление, выбор монитора, README, portable exe |

Локальная сборка: `dotnet publish src -p:PublishProfile=win-x64`.

---

## Этап 6 — Python + Qt (v2.0)

**Цель:** единое приложение на **Windows и Linux** с переиспользованием `wwwroot/wallpaper/` и совместимым `settings.json`.

**Каталог:** `python/` (`pyproject.toml`, пакет `github_wallpaper`)

### Стратегия

| Слой | Решение |
|------|---------|
| UI обоев | `wwwroot/wallpaper/` (HTML/CSS/JS) |
| WebView | `QWebEngineView` |
| Bridge | JSON-протокол `Bridge.cs` / `app.js` |
| Настройки | `settings.json` (без PAT) |
| Секреты | `keyring` (Credential Manager / Secret Service) |
| Обои | WorkerW (Win) · desktop layer X11 (Linux) |
| Трей / настройки | `QSystemTrayIcon` · Qt Widgets |

### Архитектура

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

- [x] `python/pyproject.toml` (PySide6, httpx, keyring, pydantic)
- [x] Структура пакета: `main.py`, `app.py`, `paths.py`, `single_instance.py`
- [x] `paths.py`: Windows `%APPDATA%` · Linux XDG
- [x] `QApplication` + `QSystemTrayIcon`
- [x] Single-instance (mutex / Unix socket)
- [x] Копирование `wwwroot/wallpaper/` при первом запуске

### 6.2 — Обои и WebEngine

- [x] `WallpaperWindow` — borderless `QWebEngineView`
- [x] Локальная загрузка UI (HTTP / file / qrc)
- [x] `DesktopBackend`: `apply` / `remove` / `pause` / `resume` / `set_screen`
- [x] **Windows:** `WindowsDesktopBackend` (WorkerW, `ctypes`)
- [x] **Linux:** `LinuxDesktopBackend` (X11 `_NET_WM_WINDOW_TYPE_DESKTOP`)
- [x] Выбор монитора (`QScreen` / `DisplayDeviceName`)
- [x] Пауза рендера и снижение polling

### 6.3 — Bridge (host ↔ JS)

Сообщения без изменений в `app.js`:

`pause` · `resume` · `auth:status` · `repos:init` · `repo:metadata` · `repo:commits` · `repo:pulls` · `repo:issues` · `repo:releases` · `repo:ci-run` · `repo:heatmap` · `repo:activity-feed` · `repo:poll-failed` · (JS→host) `open-url` · `page:ready`

- [x] Shim `window.chrome.webview` (QWebChannel / injection)
- [x] `bridge.py` — camelCase JSON, очередь до `page:ready`
- [x] `open-url` → `QDesktopServices.openUrl`

### 6.4 — GitHub и polling

- [x] `repo_url_parser.py`, `github_client.py`, `rate_limit_guard.py`
- [x] `repo_poller.py`, `activity_aggregator.py`
- [x] `keyring` для PAT и OAuth client secret
- [x] OAuth: Authorization Code + PKCE и Device Flow

### GitHub API (интервалы polling)

| Данные | Endpoint | Интервал |
|--------|----------|----------|
| Мета | `GET /repos/{owner}/{repo}` | 5 мин |
| Коммиты | `GET .../commits?per_page=5` | 2 мин |
| PR | `GET .../pulls?state=open&per_page=10` | 3 мин |
| Issues | `GET .../issues?state=open&per_page=10` | 3 мин |
| Релизы | `GET .../releases?per_page=5` | 10 мин |
| CI/CD | `GET .../actions/runs?per_page=1` | 3 мин |
| Heatmap | `GET .../stats/commit_activity` | 1 час |
| Лента | `GET .../events?per_page=30` | 2 мин |

### 6.5 — Настройки (Qt)

- [x] `SettingsStore` — `settings.json` (поля из `AppSettings.cs`)
- [x] `SettingsDialog` — репо, сетка, интервалы, экран, OAuth/PAT
- [x] Чтение существующего `settings.json` от v1.0
- [x] Автозапуск: registry (Win) · `.desktop` (Linux)
- [x] Пауза при полноэкранном приложении и на батарее

### 6.6 — Сборка, CI и релиз

- [x] `README`: зависимости, запуск, сборка PyInstaller
- [x] Артефакты: `GitHubWallpaper-Win-x64.zip`, `GitHubWallpaper-linux-x64.tar.gz`
- [x] GitHub Actions: тесты и сборка на `windows-latest` и `ubuntu-latest`
- [x] Release `latest` на push в `main`
- [x] Автообновление из Release `latest`

### Критерии готовности v2.0

- [ ] **Windows:** обои за иконками, трей, настройки, 3+ репо, PAT/OAuth, пауза
- [ ] **Linux (X11):** обои на рабочем столе, трей, настройки, те же виджеты
- [ ] `settings.json` и PAT переносятся с v1.0 на той же ОС
- [ ] Все сценарии из раздела [«Проверка»](#проверка) пройдены на Win и Linux
- [ ] CI зелёный на обеих платформах

### Риски

| Риск | Митигация |
|------|-----------|
| Rate limit GitHub (60/h без токена) | PAT, умные интервалы, ETag |
| `stats/*` возвращает HTTP 202 | Retry 2–5 сек, кэш |
| Qt WebEngine тяжёлый (~150 MB) | PyInstaller one-folder; документировать размер |
| `window.chrome.webview` нет в WebEngine | JS-shim + QWebChannel |
| WorkerW на новых сборках Win11 | Порт логики из `DesktopHost.cs`; fallback overlay |
| Wayland на Linux | MVP на X11; Wayland — отдельный подэтап |
| Приватные репо | PAT со scope `repo` |

---

## Фаза 2 — Будущее

Идеи после завершения этапа 6:

- GraphQL batch-запросы для 10+ репозиториев
- Per-monitor: разные наборы репо на каждом мониторе
- Кастомные темы / пользовательский CSS
- Webhook / real-time обновления
- Звуковые уведомления о важных событиях
- Поддержка Wayland (по DE)

---

## Проверка

1. Запуск portable-сборки → обои появляются за иконками (Win) / на рабочем столе (Linux X11)
2. Добавление `microsoft/vscode` → stars, commits, PR видны за 2–3 мин
3. Push в свой тестовый репо → событие в ленте
4. Pause из трея → обои замирают, polling снижается
5. Без токена → предупреждение + ограниченный режим
6. Второй монитор → обои на выбранном экране
7. Автообновление: трей → «Проверить обновления…» находит Release `latest`

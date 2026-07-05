# Дорожная карта GitHubWallpaper

Динамические обои для Windows с отслеживанием активности GitHub-репозиториев.

**Стек:** C# / .NET 8 · WebView2 · WorkerW · GitHub REST API

---

## Обзор этапов

| Этап | Название | Статус | Цель |
|------|----------|--------|------|
| 1 | [Каркас движка](#этап-1--каркас-mvp-движка) | ✅ Готов | Обои на рабочем столе + трей |
| 2 | [GitHub: один репо](#этап-2--github--один-репозиторий) | ✅ Готов | Первая живая карточка репозитория |
| 3 | [Мульти-репо и настройки](#этап-3--мульти-репо-и-настройки) | ✅ Готов | Список репо, токен, автозапуск |
| 4 | [Полная активность](#этап-4--полная-активность) | ✅ Готов | PR, issues, CI, heatmap, лента |
| 5 | [Полировка и релиз](#этап-5--полировка-и-релиз) | ✅ Готов | Стабильный exe, CI/CD, документация |
| — | [Фаза 2 (будущее)](#фаза-2--будущее) | 💡 Идеи | Расширения после v1.0 |

**Текущее состояние:** v1.0 — при push в `main` CI обновляет единственный Release `latest` с актуальным exe.

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
- [x] GitHub Actions `ci.yml` — сборка на push/PR
- [x] GitHub Actions `release.yml` — single-file exe по тегу `v*`
- [x] README: установка, создание PAT, скриншоты, FAQ
- [x] Тест на Windows 11 24H2 (сборка и publish на build 10.0.26300)

### Критерии готовности

- [x] Релизы публикуются автоматически при push в `main` (один Release `latest`, обновляется на месте)
- [x] Portable exe работает без установки .NET
- [ ] Все 5 сценариев из раздела «Проверка» пройдены (ручная проверка)

---

## Фаза 2 — Будущее

Идеи после v1.0, не входят в текущий scope:

- [x] Выбор одного монитора для отображения обоев (настройки → «Экран»)
- GraphQL batch-запросы для 10+ репозиториев
- Per-monitor: разные наборы репо на каждом мониторе
- Кастомные темы / пользовательский CSS
- Webhook / real-time обновления
- Звуковые уведомления о важных событиях
- [x] OAuth-вход через github.com (Authorization Code + PKCE и Device Flow)
- [x] Автообновление portable exe из GitHub Release `latest`

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

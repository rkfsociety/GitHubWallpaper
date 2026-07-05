# Дорожная карта GitHubWallpaper

Динамические обои для Windows с отслеживанием активности GitHub-репозиториев.

**Стек:** C# / .NET 8 · WebView2 · WorkerW · GitHub REST API

---

## Обзор этапов

| Этап | Название | Статус | Цель |
|------|----------|--------|------|
| 1 | [Каркас движка](#этап-1--каркас-mvp-движка) | 🔲 Запланирован | Обои на рабочем столе + трей |
| 2 | [GitHub: один репо](#этап-2--github--один-репозиторий) | 🔲 Запланирован | Первая живая карточка репозитория |
| 3 | [Мульти-репо и настройки](#этап-3--мульти-репо-и-настройки) | 🔲 Запланирован | Список репо, токен, автозапуск |
| 4 | [Полная активность](#этап-4--полная-активность) | 🔲 Запланирован | PR, issues, CI, heatmap, лента |
| 5 | [Полировка и релиз](#этап-5--полировка-и-релиз) | 🔲 Запланирован | Стабильный exe, CI/CD, документация |
| — | [Фаза 2 (будущее)](#фаза-2--будущее) | 💡 Идеи | Расширения после v1.0 |

---

## Этап 1 — Каркас (MVP движка)

**Цель:** приложение запускается, показывает HTML-обои за иконками рабочего стола, управляется из трея.

### Задачи

- [ ] Создать solution: `GitHubWallpaper.csproj`, `app.manifest` (PerMonitorV2 DPI)
- [ ] Подключить NuGet: `Microsoft.Web.WebView2`
- [ ] Реализовать `DesktopHost` — техника WorkerW (Progman → `0x052C`)
- [ ] Реализовать `WallpaperSurface` — borderless WebView2 в WorkerW
- [ ] Реализовать `WallpaperController` — apply / pause / resume
- [ ] Реализовать `TrayService` — иконка в трее: Настройки / Пауза / Выход
- [ ] Загрузить статичную страницу `wwwroot/wallpaper/index.html` через virtual host (`app.local`)

### Критерии готовности

- [ ] Exe запускается без установки .NET (self-contained)
- [ ] HTML-страница видна **позади** иконок рабочего стола
- [ ] Пауза из трея останавливает рендер
- [ ] Выход из трея корректно завершает процесс

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

### GitHub API (этап 2)

| Данные | Endpoint | Интервал |
|--------|----------|----------|
| Мета | `GET /repos/{owner}/{repo}` | 5 мин |
| Коммиты | `GET /repos/{owner}/{repo}/commits?per_page=5` | 2 мин |

### Критерии готовности

- [ ] Добавлен репо `microsoft/vscode` → через 2–3 мин видны stars и коммиты
- [ ] Без токена — предупреждение, лимит 60 req/h
- [ ] С токеном — `GET /user` проходит успешно

---

## Этап 3 — Мульти-репо и настройки

**Цель:** полноценное окно настроек, несколько репозиториев, сохранение конфигурации.

### Задачи

- [ ] `MainWindow` (WinForms) — UI настроек
- [ ] Список репозиториев: добавить / удалить / переупорядочить
- [ ] Поле GitHub Token + кнопка «Проверить»
- [ ] `SettingsStore` — `%APPDATA%\GitHubWallpaper\settings.json`
- [ ] Пресеты интервалов опроса: экономный / нормальный / частый
- [ ] Автозапуск (registry Run key)
- [ ] Пауза при полноэкранных приложениях (`GetForegroundWindow`)
- [ ] Пауза на батарее

### Критерии готовности

- [ ] 3+ репозитория отображаются адаптивной сеткой
- [ ] Настройки сохраняются между перезапусками
- [ ] Автозапуск работает (в трей)

---

## Этап 4 — Полная активность

**Цель:** все запрошенные виджеты активности GitHub на обоях.

### Задачи

- [ ] PR — `GET /repos/{owner}/{repo}/pulls?state=open`
- [ ] Issues — `GET /repos/{owner}/{repo}/issues?state=open` (без PR)
- [ ] Релизы — `GET /repos/{owner}/{repo}/releases?per_page=5`
- [ ] CI/CD — `GET /repos/{owner}/{repo}/actions/runs?per_page=1`
- [ ] Heatmap — `GET /repos/{owner}/{repo}/stats/commit_activity` (retry при 202)
- [ ] `ActivityAggregator` — единая лента из events + дельт polling
- [ ] Виджеты: `heatmap.js`, `feed.js`, `ci-badge.js`
- [ ] ETag / `If-None-Match` для кэширования ответов API
- [ ] Клик по карточке → открыть репо в браузере

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

- [ ] Push в тестовый репо → событие появляется в ленте
- [ ] Heatmap рисуется за 52 недели
- [ ] CI badge показывает статус последнего workflow run
- [ ] Анимация при появлении новых событий

---

## Этап 5 — Полировка и релиз

**Цель:** стабильный релиз v1.0, готовый к ежедневному использованию.

### Задачи

- [ ] Обработка ошибок: 404, private repo, сетевые сбои
- [ ] Проверка WebView2 runtime при старте + ссылка на bootstrapper
- [ ] GitHub Actions `ci.yml` — сборка на push/PR
- [ ] GitHub Actions `release.yml` — single-file exe по тегу `v*`
- [ ] README: установка, создание PAT, скриншоты, FAQ
- [ ] Тест на Windows 11 24H2

### Критерии готовности

- [ ] `v1.0.0` опубликован в Releases
- [ ] Portable exe работает без установки .NET
- [ ] Все 5 сценариев из раздела «Проверка» пройдены

---

## Фаза 2 — Будущее

Идеи после v1.0, не входят в текущий scope:

- GraphQL batch-запросы для 10+ репозиториев
- Per-monitor: разные наборы репо на каждом мониторе
- Кастомные темы / пользовательский CSS
- Webhook / real-time обновления
- Звуковые уведомления о важных событиях
- Интеграция с GitHub Notifications API

---

## Архитектура

```
Tray + Settings (WinForms)
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

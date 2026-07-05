# GitHubWallpaper

Динамические обои для Windows с живой активностью ваших GitHub-репозиториев.

> **Статус:** альфа-разработка. Этапы 1–3 завершены, этап 4 в работе. См. [дорожную карту](ROADMAP.md).

## Что это

Standalone Windows-приложение, которое рендерит интерактивные обои **позади иконок рабочего стола** и показывает актуальную информацию по выбранным репозиториям:

- последние коммиты и авторы
- открытые pull request'ы и issues
- звёзды, форки, watchers
- релизы и статус CI/CD
- heatmap активности (commit activity)
- объединённая лента событий

### Уже работает

- движок обоев (WebView2 + WorkerW) с управлением из трея
- карточка репозитория: stars, forks, последние 5 коммитов
- опрос GitHub API с учётом rate limit
- хранение PAT в Windows Credential Manager
- окно настроек: PAT, список репозиториев, интервалы опроса, автозапуск
- автопауза при полноэкранных приложениях и на батарее

### Пока в разработке

- PR, issues, CI, heatmap, лента событий
- релиз v1.0

## Стек

| Компонент | Технология |
|-----------|------------|
| Движок обоев | WebView2 + WorkerW |
| Хост-приложение | C# / .NET 8 |
| UI обоев | HTML / CSS / JavaScript |
| Данные | GitHub REST API |

## Быстрый старт

Требуется Windows 10/11 и [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/).

### Сборка и запуск

```bash
git clone https://github.com/rkfsociety/GitHubWallpaper.git
cd GitHubWallpaper
dotnet run --project src
```

Приложение свернётся в трей. На обоях появится карточка `microsoft/vscode`; данные подгрузятся за 2–3 минуты.

1. ПКМ по иконке в трее → **Настройки**.
2. Вставьте [GitHub Personal Access Token](https://github.com/settings/tokens) и нажмите **Проверить** → **Сохранить**.
3. **Пауза** / **Продолжить** — приостановить рендер обои (в т.ч. автоматически при fullscreen / батарее).

### Portable exe

```bash
dotnet publish src -p:PublishProfile=win-x64
# результат: publish/GitHubWallpaper.exe
```

Готовые сборки появятся в [Releases](https://github.com/rkfsociety/GitHubWallpaper/releases) на этапе 5.

## Настройка GitHub Token

1. Откройте [github.com/settings/tokens](https://github.com/settings/tokens).
2. Создайте token (classic: scope `public_repo`; для приватных репо: `repo`).
3. Вставьте в настройках приложения — токен хранится в Windows Credential Manager, не в файлах.

Без токена: лимит **60 запросов/час**, только публичные репозитории. При старте показывается предупреждение в трее и баннер на обоях.

## Дорожная карта

| Этап | Описание | Статус |
|------|----------|--------|
| 1 | Каркас движка (WebView2 + WorkerW + трей) | ✅ |
| 2 | GitHub API + карточка одного репо | ✅ |
| 3 | Мульти-репо, настройки, автозапуск | ✅ |
| 4 | PR, issues, CI, heatmap, лента событий | 🔲 |
| 5 | Полировка, CI/CD, релиз v1.0 | 🔲 |

Подробности: [ROADMAP.md](ROADMAP.md)

## Структура проекта

```
GitHubWallpaper/
├── src/
│   ├── Desktop/          # WorkerW, WebView2, Bridge
│   ├── GitHub/           # API-клиент, poller, парсер URL
│   ├── Settings/         # окно настроек, Credential Manager
│   ├── Tray/             # иконка в трее
│   └── Properties/       # профиль публикации win-x64
├── wwwroot/wallpaper/    # HTML/CSS/JS — UI обоев
├── ROADMAP.md
└── README.md
```

## Лицензия

MIT

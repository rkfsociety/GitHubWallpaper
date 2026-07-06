# GitHubWallpaper

Динамические обои для Windows: живые карточки GitHub-репозиториев **позади иконок рабочего стола**.

> **v1.0** (Windows, C#) — [скачать exe](https://github.com/rkfsociety/GitHubWallpaper/releases/latest) · **v2.0** (кроссплатформа, Python/PySide6) — в разработке · [дорожная карта](ROADMAP.md)

![Карточки репозиториев на рабочем столе](docs/screenshots/wallpaper.svg)

## Возможности

Коммиты, PR, issues, релизы, CI, heatmap и лента событий · несколько репозиториев · выбор монитора · OAuth или PAT · автообновление из Release.

## Установка

**Требования:** Windows 10/11, [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (обычно уже есть).

1. Скачайте `GitHubWallpaper.exe` из [Release `latest`](https://github.com/rkfsociety/GitHubWallpaper/releases/latest).
2. Запустите — при первом старте приложение скопируется в `%APPDATA%\GitHubWallpaper\` и создаст ярлыки.
3. Иконка в трее → **Настройки** → войдите через GitHub или вставьте PAT → добавьте репозитории.

Сборка self-contained, .NET на машине не нужен. Из исходников: `dotnet run --project src` · portable: `dotnet publish src -p:PublishProfile=win-x64`.

## Авторизация

**OAuth (рекомендуется):** в настройках — **Войти через GitHub**. Для своего OAuth App: callback `http://127.0.0.1:8791/callback`, включить Device Flow.

**PAT:** хранится в Windows Credential Manager. [Fine-grained](https://github.com/settings/tokens?type=beta) или [classic](https://github.com/settings/tokens) с read-доступом к репозиториям; для приватных — scope `repo`. Без токена — 60 запросов/час, только публичные репо.

![Окно настроек](docs/screenshots/settings.svg)

## Частые вопросы

| Проблема | Решение |
|----------|---------|
| Чёрный экран | Установите WebView2 Runtime |
| Не тот монитор | Настройки → **Экран** |
| 404 / нет приватного репо | PAT со scope `repo` |
| Данные не обновляются | Проверьте сеть и лимит API; пресет **Экономный** |

Настройки: `%APPDATA%\GitHubWallpaper\settings.json`. Удаление: трей → **Выход**, затем папка AppData и запись `GitHubWallpaper` в Credential Manager.

## Статус

| Версия | Платформа | Стек | Статус |
|--------|-----------|------|--------|
| v1.0 | Windows 10/11 | C# / .NET 8 · WebView2 | ✅ [Release `latest`](https://github.com/rkfsociety/GitHubWallpaper/releases/latest) |
| v2.0 | Windows + Linux | Python · PySide6 · Qt WebEngine | 🔄 [Этап 6](ROADMAP.md#этап-6--python--qt-v20-кроссплатформа), ветка `python-qt` |

## Разработка

**v1.0 (C#):** `dotnet run --project src` · portable: `dotnet publish src -p:PublishProfile=win-x64`

**v2.0 (Python):** каталог `python/` (см. [ROADMAP, этап 6](ROADMAP.md#этап-6--python--qt-v20-кроссплатформа))

## Лицензия

MIT

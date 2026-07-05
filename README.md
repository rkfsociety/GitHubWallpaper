# GitHubWallpaper

Динамические обои для Windows с живой активностью ваших GitHub-репозиториев.

> **Статус:** v1.0 — готов к ежедневному использованию. См. [дорожную карту](ROADMAP.md).

## Что это

Standalone Windows-приложение, которое рендерит интерактивные обои **позади иконок рабочего стола** и показывает актуальную информацию по выбранным репозиториям:

- последние коммиты и авторы
- открытые pull request'ы и issues
- звёзды, форки, watchers
- релизы и статус CI/CD
- heatmap активности (commit activity)
- объединённая лента событий

![Карточки репозиториев на рабочем столе](docs/screenshots/wallpaper.svg)

## Установка

### Требования

- Windows 10 или Windows 11 (протестировано на **Windows 11 24H2**)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) — на большинстве систем уже установлен; при первом запуске приложение предложит скачать bootstrapper, если runtime отсутствует

### Portable exe (рекомендуется)

1. Скачайте `GitHubWallpaper.exe` из [последнего релиза](https://github.com/rkfsociety/GitHubWallpaper/releases/latest) — один Release обновляется автоматически при каждом push в `main`.
2. Запустите двойным щелчком из любой папки (например, «Загрузки»).
3. При **первом запуске** приложение само скопируется в `%APPDATA%\GitHubWallpaper\`, перезапустится оттуда и создаст ярлыки на **рабочем столе** и в меню **«Пуск»**. Файл в загрузках можно удалить — рабочая копия останется в AppData.
4. Приложение свернётся в трей; обои появятся за иконками рабочего стола.
5. ПКМ по иконке в трее → **Проверить обновления…** (автопроверка раз в сутки).

Сборка **self-contained**: .NET на машине устанавливать не нужно.

### Сборка из исходников

```bash
git clone https://github.com/rkfsociety/GitHubWallpaper.git
cd GitHubWallpaper
dotnet run --project src
```

Portable exe:

```bash
dotnet publish src -p:PublishProfile=win-x64
# результат: publish/GitHubWallpaper.exe
```

Версия в exe: `1.0.<номер сборки>` при push в `main`. В GitHub всегда один Release с тегом `latest`. Локально без `-p:Version` — `1.0.0-dev`.

## Быстрый старт

1. ПКМ по иконке в трее → **Настройки**.
2. Нажмите **Войти через GitHub** — откроется [github.com](https://github.com/login/oauth/authorize) в браузере, подтвердите доступ.
3. Либо вставьте [Personal Access Token](#создание-github-pat) вручную → **Проверить** → **Сохранить**.
3. Добавьте репозитории (`owner/repo` или URL) — по умолчанию уже есть `microsoft/vscode`.
4. Данные подгрузятся за 2–3 минуты.
5. **Пауза** / **Продолжить** в трее — приостановить рендер (в т.ч. автоматически при fullscreen / батарее).

![Окно настроек](docs/screenshots/settings.svg)

## Авторизация GitHub

### Вход через браузер (рекомендуется)

1. **Настройки** → **Создать OAuth App** (или [github.com/settings/applications/new](https://github.com/settings/applications/new)).
2. Заполните:
   - **Application name:** GitHub Wallpaper
   - **Homepage URL:** `https://github.com/rkfsociety/GitHubWallpaper`
   - **Authorization callback URL:** `http://127.0.0.1:8791/callback`
   - Включите **Enable Device Flow**
3. Скопируйте **Client ID** (строка вида `Ov23li…`, не номер из URL страницы) в настройки.
4. Для входа через браузер — **Client Secret** (кнопка Generate a new client secret). Для **Device Flow** secret не нужен.
5. Нажмите **Войти через GitHub** — откроется github.com, подтвердите доступ.

При проблемах с callback используйте ссылку **Вход по коду устройства** (`github.com/login/device`).

### Создание GitHub PAT

Токен хранится в **Windows Credential Manager**, не в файлах на диске.

### Fine-grained token (рекомендуется)

1. Откройте [github.com/settings/tokens?type=beta](https://github.com/settings/personal-access-tokens).
2. **Generate new token** → выберите нужные репозитории (или все).
3. Permissions: **Metadata** (read), **Contents** (read), **Issues** (read), **Pull requests** (read), **Actions** (read) — для полной картины активности.
4. Скопируйте токен и вставьте в настройках приложения.

### Classic token

1. Откройте [github.com/settings/tokens](https://github.com/settings/tokens).
2. **Generate new token (classic)**.
3. Scopes:
   - `public_repo` — только публичные репозитории;
   - `repo` — включая **приватные** репозитории.
4. Вставьте токен в настройках → **Проверить** → **Сохранить**.

### Без токена

Лимит **60 запросов/час**, только публичные репозитории. При старте — предупреждение в трее и баннер на обоях.

## FAQ

**Обои не появляются / чёрный экран**  
Убедитесь, что установлен WebView2 Runtime. При запуске без runtime приложение покажет диалог со ссылкой на [Evergreen Bootstrapper](https://go.microsoft.com/fwlink/p/?LinkId=2124703).

**Репозиторий не найден (404)**  
Проверьте правильность `owner/repo`. Для приватных репозиториев GitHub возвращает 404 без токена — добавьте PAT со scope `repo`.

**Нет доступа к приватному репо**  
Создайте classic PAT со scope `repo` или fine-grained token с доступом к этому репозиторию.

**Данные не обновляются**  
Проверьте интернет и лимит API (баннер / настройки). При исчерпании лимита опрос автоматически приостанавливается до сброса.

**Высокая нагрузка на API**  
В настройках выберите пресет **Экономный** или уменьшите число репозиториев. Приложение использует ETag-кэш — неизменённые ответы не загружаются повторно.

**Где хранятся настройки?**  
`%APPDATA%\GitHubWallpaper\settings.json` — список репозиториев и параметры. PAT — только в Credential Manager.

**Как удалить?**  
Закройте через трей → **Выход**, удалите exe. Опционально: очистите `%APPDATA%\GitHubWallpaper\` и PAT в Credential Manager (запись `GitHubWallpaper`).

## Проверка перед релизом

| # | Сценарий | Ожидание |
|---|----------|----------|
| 1 | Запуск exe | Обои за иконками рабочего стола |
| 2 | `microsoft/vscode` | Stars, commits, PR за 2–3 мин |
| 3 | Push в свой тестовый репо | Событие в ленте |
| 4 | Пауза из трея | Обои замирают, polling снижается |
| 5 | Без токена | Предупреждение + ограниченный режим |

## Стек

| Компонент | Технология |
|-----------|------------|
| Движок обоев | WebView2 + WorkerW |
| Хост-приложение | C# / .NET 8 |
| UI обоев | HTML / CSS / JavaScript |
| Данные | GitHub REST API |

## Дорожная карта

| Этап | Описание | Статус |
|------|----------|--------|
| 1 | Каркас движка (WebView2 + WorkerW + трей) | ✅ |
| 2 | GitHub API + карточка одного репо | ✅ |
| 3 | Мульти-репо, настройки, автозапуск | ✅ |
| 4 | PR, issues, CI, heatmap, лента событий | ✅ |
| 5 | Полировка, CI/CD, релиз v1.0 | ✅ |

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
├── .github/workflows/    # CI и Release
├── docs/screenshots/     # иллюстрации для README
├── ROADMAP.md
└── README.md
```

## Лицензия

MIT

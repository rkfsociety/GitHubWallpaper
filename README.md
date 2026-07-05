# GitHubWallpaper

Динамические обои для Windows с живой активностью ваших GitHub-репозиториев.

> **Статус:** в разработке (этап 1). См. [дорожную карту](ROADMAP.md).

## Что это

Standalone Windows-приложение, которое рендерит интерактивные обои **позади иконок рабочего стола** и показывает актуальную информацию по выбранным репозиториям:

- последние коммиты и авторы
- открытые pull request'ы и issues
- звёзды, форки, watchers
- релизы и статус CI/CD
- heatmap активности (commit activity)
- объединённая лента событий

## Стек

| Компонент | Технология |
|-----------|------------|
| Движок обоев | WebView2 + WorkerW |
| Хост-приложение | C# / .NET 8 |
| UI обоев | HTML / CSS / JavaScript |
| Данные | GitHub REST API |

## Быстрый старт (когда будет готово)

1. Скачайте `GitHubWallpaper.exe` из [Releases](https://github.com/rkfsociety/GitHubWallpaper/releases).
2. Запустите — приложение свернётся в трей.
3. ПКМ по иконке → **Настройки** → добавьте репозитории (`owner/repo` или полный URL).
4. Укажите [GitHub Personal Access Token](https://github.com/settings/tokens) для стабильного опроса API.

## Сборка из исходников

Требуется [.NET 8 SDK](https://dotnet.microsoft.com/download) на Windows.

```bash
git clone https://github.com/rkfsociety/GitHubWallpaper.git
cd GitHubWallpaper
dotnet run
```

```bash
# Portable single-file exe
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish
```

## Настройка GitHub Token

1. Откройте [github.com/settings/tokens](https://github.com/settings/tokens).
2. Создайте token (classic: scope `public_repo`; для приватных репо: `repo`).
3. Вставьте в настройках приложения — токен хранится в Windows Credential Manager, не в файлах.

Без токена: лимит **60 запросов/час**, только публичные репозитории.

## Дорожная карта

| Этап | Описание | Статус |
|------|----------|--------|
| 1 | Каркас движка (WebView2 + WorkerW + трей) | 🔲 |
| 2 | GitHub API + карточка одного репо | 🔲 |
| 3 | Мульти-репо, настройки, автозапуск | 🔲 |
| 4 | PR, issues, CI, heatmap, лента событий | 🔲 |
| 5 | Полировка, CI/CD, релиз v1.0 | 🔲 |

Подробности: [ROADMAP.md](ROADMAP.md)

## Структура проекта (план)

```
GitHubWallpaper/
├── src/                  # C# — движок, GitHub API, настройки
├── wwwroot/wallpaper/    # HTML/CSS/JS — UI обоев
├── .github/workflows/    # CI/CD
├── ROADMAP.md
└── README.md
```

## Лицензия

MIT

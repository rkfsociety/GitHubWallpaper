# GitHub Wallpaper (Wallpaper Engine)

Web-обои для **Wallpaper Engine**: карточки GitHub-репозиториев с коммитами, PR, issues, релизами, CI и heatmap.

## Установка

1. В Wallpaper Engine выберите **Open Wallpaper** → **Open from file**.
2. Укажите файл `wallpaper_engine/index.html` (или откройте папку `wallpaper_engine/` через редактор WE).

## Настройка

Откройте настройки обоев в Wallpaper Engine и заполните:

- **GitHub token (PAT)**: персональный токен GitHub.
- **Репозиторий N (owner/repo)**: например `microsoft/vscode`.
- **Колонки/Ряды**: размер сетки.
- **Переключатели секций**: что показывать на карточке (CI/релиз/heatmap/лента и т.д.).

### Токен (PAT)

Рекомендуется **Fine-grained PAT** с read-only доступом. Без токена GitHub ограничивает запросы до **60/час**.

## Структура

- `wallpaper_engine/`: готовый проект Wallpaper Engine (HTML wallpaper)
  - `project.json`: настройки/свойства
  - `we-adapter.js`: получение данных из GitHub API и доставка в UI

## Лицензия

MIT

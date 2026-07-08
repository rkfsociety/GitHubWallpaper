# GitHub Wallpaper (Wallpaper Engine)

Web-обои для **Wallpaper Engine**: карточки GitHub-репозиториев с коммитами, PR, issues, релизами, CI и heatmap.

## Установка

**Важно:** если открыть только `index.html` из клона репозитория, WE показывает заглушку «Местный веб» без иконки. Нужен `project.json` и `preview.jpg` в той же папке.

### Рекомендуемый способ

```powershell
cd wallpaper_engine
.\install_to_we.ps1
```

Скрипт копирует проект в `wallpaper_engine\projects\myprojects\GitHubWallpaper\`. Затем в WE:

1. Нажмите **Удалить из истории** у старой записи `index.html` (если есть).
2. **Open Wallpaper** → **Open from file** → выберите  
   `...\wallpaper_engine\projects\myprojects\GitHubWallpaper\index.html`

Должны появиться название **GitHub Wallpaper (Wallpaper Engine)** и иконка.

### Альтернатива

**Create Wallpaper** → перетащите `wallpaper_engine/index.html` в окно редактора (импорт в myprojects).

## Настройка

Откройте настройки обоев в Wallpaper Engine и заполните:

- **GitHub token (PAT)**: персональный токен GitHub.
- **Репозиторий N (owner/repo)**: например `microsoft/vscode`.
- **Колонки/Ряды**: размер сетки.
- **Переключатели секций**: что показывать на карточке (CI/релиз/heatmap/лента и т.д.).

### Токен (PAT)

Рекомендуется **fine-grained PAT** (только чтение). При создании:

- **Repository access** — только репозитории, которые показываете в обоях (или «All repositories»).
- **Permissions → Repository** (все **Read-only**):
  - **Metadata** (обычно включён по умолчанию)
  - **Contents** — коммиты, релизы, heatmap
  - **Issues**
  - **Pull requests**
  - **Actions** — статус CI

Без токена GitHub даёт **60 запросов/час** и только **публичные** репозитории. Для приватных репо токен обязателен.

Классический PAT (альтернатива): scope `public_repo` (публичные) или `repo` (включая приватные).

## Структура

- `wallpaper_engine/`: готовый проект Wallpaper Engine (HTML wallpaper)
  - `project.json`: настройки/свойства
  - `preview.jpg`: иконка в библиотеке WE
  - `install_to_we.ps1`: копирование в myprojects
  - `we-adapter.js`: получение данных из GitHub API и доставка в UI

## Лицензия

MIT

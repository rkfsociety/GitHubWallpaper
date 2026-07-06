# GitHubWallpaper

Динамические обои: карточки GitHub-репозиториев **позади иконок рабочего стола** (Windows + Linux).

> [Скачать v2.0](https://github.com/rkfsociety/GitHubWallpaper/releases/latest) · [дорожная карта](ROADMAP.md)

![Карточки репозиториев на рабочем столе](docs/screenshots/wallpaper.svg)

Коммиты, PR, issues, релизы, CI, heatmap, лента событий · несколько репо · выбор монитора · OAuth/PAT · автообновление.

## Установка

1. Скачайте из [Release `latest`](https://github.com/rkfsociety/GitHubWallpaper/releases/latest):
   - **Windows** — `GitHubWallpaper.exe`
   - **Linux (X11)** — `GitHubWallpaper`
2. Запустите файл. При первом запуске приложение скачает компоненты (~150 МБ) в каталог данных и установит launcher в `%APPDATA%\GitHubWallpaper\` (Win) или `~/.config/GitHubWallpaper/` (Linux).
3. Трей → **Настройки** → GitHub OAuth или PAT → добавьте репозитории.

Без Python/.NET на машине. Обновления: трей → «Проверить обновления…».

**Авторизация:** OAuth в настройках (рекомендуется) или [PAT](https://github.com/settings/tokens) с read-доступом (`repo` для приватных). Без токена — 60 req/h, только публичные репо.

**Настройки:** `%APPDATA%\GitHubWallpaper\` (Win) · `~/.config/GitHubWallpaper/` (Linux).

| Проблема | Решение |
|----------|---------|
| Чёрный экран | Перезапуск; на Linux — X11 и OpenGL |
| Не тот монитор | Настройки → Экран |
| Приватный репо / 404 | PAT со scope `repo` |
| Нет обновлений данных | Сеть, лимит API, пресет «Экономный» |

## Разработка

Каталог `python/`, Python 3.11+, PySide6 (Qt WebEngine).

```bash
cd python
python -m pip install -e .
python -m github_wallpaper          # из исходников
python -m pip install -e ".[dev]" # + PyInstaller
python scripts/build_release.py    # → python/publish/
```

HTML/CSS/JS обоев — `wwwroot/wallpaper/`. CI на push в `main` публикует Release `latest`. Архив C# v1.0 — `src/`.

## Лицензия

MIT

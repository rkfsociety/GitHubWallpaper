# GitHubWallpaper

Динамические обои: карточки GitHub-репозиториев **позади иконок рабочего стола** (Windows + Linux).

> [Скачать v2.0](https://github.com/rkfsociety/GitHubWallpaper/releases/latest) · [дорожная карта](ROADMAP.md)

![Карточки репозиториев на рабочем столе](docs/screenshots/wallpaper.svg)

Коммиты, PR, issues, релизы, CI, heatmap, лента событий · несколько репо · выбор монитора · OAuth/PAT · автообновление.

## Установка

### Linux (X11)

```bash
curl -fsSL -o GitHubWallpaper https://github.com/rkfsociety/GitHubWallpaper/releases/download/latest/GitHubWallpaper \
  && chmod +x GitHubWallpaper \
  && ./GitHubWallpaper
```

При первом запуске скачаются компоненты (~150 МБ) в `~/.config/GitHubWallpaper/`. Требуется X11, OpenGL и glibc 2.31+ (Debian 11 / Ubuntu 20.04+).

### Windows

Скачайте [`GitHubWallpaper.exe`](https://github.com/rkfsociety/GitHubWallpaper/releases/download/latest/GitHubWallpaper.exe) из [Release `latest`](https://github.com/rkfsociety/GitHubWallpaper/releases/latest) и запустите. Компоненты установятся в `%APPDATA%\GitHubWallpaper\`.

### Первый запуск

Трей → **Настройки** → GitHub OAuth или PAT → добавьте репозитории.

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

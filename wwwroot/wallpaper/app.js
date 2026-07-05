(function () {
  const overlay = document.getElementById("pause-overlay");
  const repoGrid = document.getElementById("repo-grid");
  const authBanner = document.getElementById("auth-banner");
  const wallpaperContent = document.getElementById("wallpaper-content");
  const wallpaperRoot = document.querySelector(".wallpaper");

  const MANY_REPOS_THRESHOLD = 2;
  const DEFAULT_GRID_COLUMNS = 3;
  const DEFAULT_GRID_ROWS = 2;
  const MIN_CONTENT_ZOOM = 0.4;
  const MIN_CARD_CONTENT_ZOOM = 0.52;
  const FIT_RETRY_DELAYS_MS = [0, 120, 400, 1200];

  const state = {
    repos: Object.create(null),
  };

  const cardElements = new Map();

  const layoutState = {
    density: "default",
    columns: DEFAULT_GRID_COLUMNS,
    rows: DEFAULT_GRID_ROWS,
    commitLimit: 5,
  };

  const gridSettings = {
    columns: DEFAULT_GRID_COLUMNS,
    rows: DEFAULT_GRID_ROWS,
  };

  const displaySettings = {
    description: true,
    stats: true,
    ci: true,
    release: true,
    heatmap: true,
    feed: true,
    pullRequests: true,
    issues: true,
    commits: true,
  };

  const layoutPresets = {
    default: { minCardRem: 20, estCardPx: 640, commitLimit: 5, heroPx: 220 },
    compact: { minCardRem: 18, estCardPx: 560, commitLimit: 4, heroPx: 100 },
    dense: { minCardRem: 16, estCardPx: 480, commitLimit: 3, heroPx: 80 },
    tight: { minCardRem: 14, estCardPx: 400, commitLimit: 2, heroPx: 64 },
  };

  let lastViewport = null;
  let fitContentFrame = 0;
  let contentResizeObserver = null;
  let isFittingContent = false;

  const icons = {
    star:
      '<svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M8 .25a.75.75 0 0 1 .673.418l1.882 3.815 4.21.612a.75.75 0 0 1 .416 1.279l-3.046 2.97.719 4.192a.751.751 0 0 1-1.088.791L8 12.347l-3.766 1.98a.75.75 0 0 1-1.088-.79l.72-4.194L.818 6.374a.75.75 0 0 1 .416-1.28l4.21-.611L7.327.668A.75.75 0 0 1 8 .25Z"/></svg>',
    fork:
      '<svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M5 5.372v.878c0 .414.336.75.75.75h4.5a.75.75 0 0 0 .75-.75v-.878a2.25 2.25 0 1 1 1.5 0v.878a2.25 2.25 0 0 1-2.25 2.25h-1.5v2.128a2.251 2.251 0 1 1-1.5 0V8.5h-1.5A2.25 2.25 0 0 1 3.5 6.25v-.878a2.25 2.25 0 1 1 1.5 0ZM5 3.25a.75.75 0 1 0-1.5 0 .75.75 0 0 0 1.5 0Zm6.75.75a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm-3 8.75a.75.75 0 1 0-1.5 0 .75.75 0 0 0 1.5 0Z"/></svg>',
    pr:
      '<svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M1.5 3.25a2.25 2.25 0 1 1 3 2.122v5.256a2.251 2.251 0 1 1-1.5 0V5.372A2.25 2.25 0 0 1 1.5 3.25Zm5.677-.177L9.573.677A.25.25 0 0 1 10 .854V2.5h1A2.5 2.5 0 0 1 13.5 5v5.628a2.251 2.251 0 1 1-1.5 0V5A1 1 0 0 0 11 4H9.5v6.128a2.251 2.251 0 1 1-1.5 0V5.372a2.249 2.249 0 0 1 1.677-.299Z"/></svg>',
    issue:
      '<svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M8 9.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3Z"/><path d="M8 0a8 8 0 1 1 0 16A8 8 0 0 1 8 0ZM1.5 8a6.5 6.5 0 1 0 13 0 6.5 6.5 0 0 0-13 0Z"/></svg>',
  };

  function repoKey(owner, repo) {
    return `${owner}/${repo}`;
  }

  function ensureRepo(owner, repo) {
    const key = repoKey(owner, repo);
    if (!state.repos[key]) {
      state.repos[key] = { owner, repo };
    }

    return state.repos[key];
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function formatCount(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) {
      return "0";
    }

    if (number >= 1_000_000) {
      return `${(number / 1_000_000).toFixed(1).replace(/\.0$/, "")}M`;
    }

    if (number >= 10_000) {
      return `${Math.round(number / 1_000)}k`;
    }

    if (number >= 1_000) {
      return `${(number / 1_000).toFixed(1).replace(/\.0$/, "")}k`;
    }

    return number.toLocaleString("ru-RU");
  }

  function formatRelativeDate(value) {
    if (!value) {
      return "";
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return "";
    }

    const diffMs = date.getTime() - Date.now();
    const diffMinutes = Math.round(diffMs / 60_000);
    const ranges = [
      { limit: 60, divisor: 1, unit: "minute" },
      { limit: 24 * 60, divisor: 60, unit: "hour" },
      { limit: 7 * 24 * 60, divisor: 24 * 60, unit: "day" },
      { limit: 30 * 24 * 60, divisor: 7 * 24 * 60, unit: "week" },
      { limit: 365 * 24 * 60, divisor: 30 * 24 * 60, unit: "month" },
    ];

    const formatter = new Intl.RelativeTimeFormat("ru", { numeric: "auto" });
    const absoluteMinutes = Math.abs(diffMinutes);

    for (const range of ranges) {
      if (absoluteMinutes < range.limit) {
        const value = Math.round(diffMinutes / range.divisor) || 0;
        return formatter.format(value, range.unit);
      }
    }

    return formatter.format(Math.round(diffMinutes / (365 * 24 * 60)), "year");
  }

  function shortSha(sha) {
    return String(sha || "").slice(0, 7);
  }

  function setPaused(paused) {
    document.body.classList.toggle("is-paused", paused);
    overlay.hidden = !paused;
  }

  function setAuthStatus(payload) {
    if (!authBanner || !payload) {
      return;
    }

    if (payload.hasToken) {
      authBanner.hidden = true;
      authBanner.textContent = "";
      return;
    }

    authBanner.hidden = false;
    authBanner.textContent =
      payload.message ||
      "GitHub token не задан — лимит 60 запросов/час. Настройки → «Войти через GitHub».";
  }

  const EMPTY_SLOT_PREFIX = "__empty__";

  function emptySlotKey(slotIndex) {
    return `${EMPTY_SLOT_PREFIX}${slotIndex}`;
  }

  function initRepos(repositories) {
    if (!Array.isArray(repositories)) {
      return;
    }

    const newKeys = new Set();
    const gridItems = [];

    repositories.forEach((item, slotIndex) => {
      if (!item?.owner || !item?.repo) {
        gridItems.push({ type: "empty", key: emptySlotKey(slotIndex) });
        return;
      }

      const key = repoKey(item.owner, item.repo);
      newKeys.add(key);
      gridItems.push({ type: "repo", key, entry: ensureRepo(item.owner, item.repo) });
    });

    for (const key of Object.keys(state.repos)) {
      if (newKeys.has(key)) {
        continue;
      }

      delete state.repos[key];
      const card = cardElements.get(key);
      if (card) {
        card.remove();
        cardElements.delete(key);
      }
    }

    for (const [key, card] of cardElements.entries()) {
      if (!key.startsWith(EMPTY_SLOT_PREFIX)) {
        continue;
      }

      const stillUsed = gridItems.some((item) => item.type === "empty" && item.key === key);
      if (!stillUsed) {
        card.remove();
        cardElements.delete(key);
      }
    }

    repoGrid.replaceChildren();

    for (const item of gridItems) {
      if (item.type === "empty") {
        let card = cardElements.get(item.key);
        if (!card) {
          card = document.createElement("div");
          card.className = "repo-grid__spacer";
          card.setAttribute("aria-hidden", "true");
          cardElements.set(item.key, card);
        }

        repoGrid.appendChild(card);
        continue;
      }

      let card = cardElements.get(item.key);
      if (!card) {
        card = document.createElement("div");
        cardElements.set(item.key, card);
      }

      repoGrid.appendChild(card);
      mountRepoCard(item.entry);
    }

    updateLayout(lastViewport);
  }

  function dispatchBridgeEvent(name, detail) {
    document.dispatchEvent(new CustomEvent(name, { detail }));
  }

  function renderMiniList(items, emptyText) {
    if (!items) {
      return `<p class="repo-card__placeholder">${emptyText}</p>`;
    }

    if (items.length === 0) {
      return `<p class="repo-card__placeholder">Пусто</p>`;
    }

    const rows = items
      .slice(0, 3)
      .map((item) => {
        const title = escapeHtml(item.title || "Без названия");
        const number = item.number ? `#${item.number}` : "";
        return `<li><span class="repo-card__mini-item"><span class="repo-card__mini-number">${escapeHtml(number)}</span>${title}</span></li>`;
      })
      .join("");

    return `<ul class="repo-card__mini-list">${rows}</ul>`;
  }

  function renderCommits(entry) {
    const commits = entry.commits;

    if (!commits) {
      return '<p class="repo-card__placeholder">Загрузка коммитов…</p>';
    }

    if (commits.length === 0) {
      return '<p class="repo-card__placeholder">Коммитов пока нет</p>';
    }

    const items = commits
      .slice(0, layoutState.commitLimit)
      .map((commit) => {
        const sha = shortSha(commit.sha);
        const message = escapeHtml(commit.message || "Без сообщения");
        const author = escapeHtml(commit.authorName || "Неизвестный автор");
        const when = escapeHtml(formatRelativeDate(commit.authorDate));

        return `
          <li class="repo-card__commit">
            <span class="repo-card__commit-sha">${sha}</span>
            <span class="repo-card__commit-message" title="${message}">${message}</span>
            <span class="repo-card__commit-meta">${author}${when ? ` · ${when}` : ""}</span>
          </li>
        `;
      })
      .join("");

    return `<ul class="repo-card__commit-list">${items}</ul>`;
  }

  function formatPollError(payload) {
    if (!payload) {
      return "Ошибка опроса";
    }

    const titles = {
      not_found: "Репозиторий не найден",
      forbidden: "Нет доступа к репозиторию",
      unauthorized: "Токен недействителен",
      rate_limited: "Лимит GitHub API исчерпан",
      network: "Сеть недоступна",
      server_error: "Ошибка сервера GitHub",
    };

    const title = titles[payload.code] || "Ошибка опроса";
    const hint = payload.hint ? ` ${payload.hint}` : "";
    return `${title}. ${payload.message || ""}${hint}`.trim();
  }

  function clearRepoError(entry) {
    if (entry.lastError) {
      delete entry.lastError;
    }
  }

  function renderRepoCard(entry) {
    const key = repoKey(entry.owner, entry.repo);
    const metadata = entry.metadata;
    const fullName = metadata?.fullName || key;
    const description =
      displaySettings.description && metadata?.description
        ? `<p class="repo-card__description">${escapeHtml(metadata.description)}</p>`
        : "";
    const stars = formatCount(metadata?.stars ?? 0);
    const forks = formatCount(metadata?.forks ?? 0);
    const pullCount = formatCount(entry.pulls?.length ?? 0);
    const issueCount = formatCount(metadata?.openIssues ?? entry.issues?.length ?? 0);
    const ciBadge = displaySettings.ci ? window.WallpaperCiBadge?.render(entry.ciRun) || "" : "";
    const heatmapBlock = displaySettings.heatmap
      ? `
        <section class="repo-card__heatmap">
          <h3 class="repo-card__section-title">Активность</h3>
          ${window.WallpaperHeatmap?.render(entry.heatmap?.weeks) || ""}
        </section>`
      : "";
    const feedBlock = displaySettings.feed
      ? `
        <section class="repo-card__feed">
          <h3 class="repo-card__section-title">Лента</h3>
          ${window.WallpaperFeed?.render(entry.activityFeed) || ""}
        </section>`
      : "";
    const error = entry.lastError
      ? `<p class="repo-card__error" role="alert">${escapeHtml(formatPollError(entry.lastError))}</p>`
      : "";
    const loadingClass = metadata ? "" : " is-loading";
    const latestRelease = entry.releases?.[0];
    const releaseLine =
      displaySettings.release && latestRelease
        ? `<p class="repo-card__release">Последний релиз: <span class="repo-card__release-name">${escapeHtml(latestRelease.name || latestRelease.tagName)}</span></p>`
        : "";
    const statsBlock = displaySettings.stats
      ? `
        <div class="repo-card__stats">
          <span class="repo-card__stat" title="Звёзды">${icons.star}<span>${stars}</span></span>
          <span class="repo-card__stat" title="Форки">${icons.fork}<span>${forks}</span></span>
          <span class="repo-card__stat" title="Открытые PR">${icons.pr}<span>${pullCount}</span></span>
          <span class="repo-card__stat" title="Открытые issues">${icons.issue}<span>${issueCount}</span></span>
        </div>`
      : "";

    let columnsBlock = "";
    if (displaySettings.pullRequests || displaySettings.issues) {
      const pullSection = displaySettings.pullRequests
        ? `
          <section class="repo-card__mini-section">
            <h3 class="repo-card__section-title">Pull requests</h3>
            ${renderMiniList(entry.pulls, "Загрузка PR…")}
          </section>`
        : "";
      const issueSection = displaySettings.issues
        ? `
          <section class="repo-card__mini-section">
            <h3 class="repo-card__section-title">Issues</h3>
            ${renderMiniList(entry.issues, "Загрузка issues…")}
          </section>`
        : "";

      columnsBlock = `
        <div class="repo-card__columns">
          ${pullSection}
          ${issueSection}
        </div>`;
    }

    const commitsBlock = displaySettings.commits
      ? `
        <section class="repo-card__commits">
          <h3 class="repo-card__section-title">Последние коммиты</h3>
          ${renderCommits(entry)}
        </section>`
      : "";

    return `
      <article class="repo-card${loadingClass}" data-repo-key="${escapeHtml(key)}">
        <header class="repo-card__header">
          <div class="repo-card__title-row">
            <h2 class="repo-card__title">
              <span class="repo-card__title-text">${escapeHtml(fullName)}</span>
            </h2>
            ${ciBadge}
          </div>
          ${description}
        </header>

        <div class="repo-card__fit">
          <div class="repo-card__fit-inner">
            ${statsBlock}
            ${releaseLine}
            ${error}
            ${heatmapBlock}
            ${feedBlock}
            ${columnsBlock}
            ${commitsBlock}
          </div>
        </div>
      </article>
    `;
  }

  function mountRepoCard(entry, options = {}) {
    const key = repoKey(entry.owner, entry.repo);
    let card = cardElements.get(key);

    if (!card) {
      card = document.createElement("div");
      cardElements.set(key, card);
      repoGrid.appendChild(card);
    }

    card.innerHTML = renderRepoCard(entry);

    const feedSection = card.querySelector(".repo-card__feed");
    window.WallpaperFeed?.markNewItemsAnimated(feedSection);

    if (!options.deferLayout) {
      scheduleEqualizeAndFit();
    }
  }

  function refreshRepoCard(owner, repo) {
    const entry = state.repos[repoKey(owner, repo)];
    if (!entry) {
      return;
    }

    mountRepoCard(entry);
  }

  function equalizeRepoCardHeights() {
    if (!repoGrid) {
      return;
    }

    const columns = Math.max(1, layoutState.columns);
    const rows = Math.max(1, layoutState.rows);
    const slots = [...repoGrid.children];

    for (const slot of slots) {
      slot.style.minHeight = "";
      const card = slot.querySelector(".repo-card");
      if (card) {
        card.style.minHeight = "";
        const fitInner = card.querySelector(".repo-card__fit-inner");
        if (fitInner) {
          fitInner.style.zoom = "";
        }
      }
    }

    if (slots.length <= 1) {
      fitCardContents();
      return;
    }

    for (let rowIndex = 0; rowIndex < rows; rowIndex += 1) {
      const rowSlots = slots.slice(rowIndex * columns, (rowIndex + 1) * columns);
      if (rowSlots.length === 0) {
        continue;
      }

      let maxHeight = 0;
      for (const slot of rowSlots) {
        maxHeight = Math.max(maxHeight, slot.offsetHeight);
      }

      if (maxHeight <= 0) {
        continue;
      }

      for (const slot of rowSlots) {
        slot.style.minHeight = `${maxHeight}px`;
      }
    }

    fitCardContents();
  }

  function fitCardContents() {
    const cards = document.querySelectorAll(".repo-card");
    for (const card of cards) {
      const fit = card.querySelector(".repo-card__fit");
      const inner = card.querySelector(".repo-card__fit-inner");
      if (!fit || !inner) {
        continue;
      }

      inner.style.zoom = "1";
      forceReflow();

      const available = fit.clientHeight;
      if (available <= 0) {
        continue;
      }

      const natural = inner.scrollHeight;
      if (natural <= available + 1) {
        continue;
      }

      const zoom = Math.max(MIN_CARD_CONTENT_ZOOM, available / natural);
      inner.style.zoom = String(Math.round(zoom * 1000) / 1000);
    }
  }

  function refreshAllRepoCards() {
    for (const entry of Object.values(state.repos)) {
      mountRepoCard(entry, { deferLayout: true });
    }

    scheduleEqualizeAndFit();
  }

  function scheduleEqualizeAndFit() {
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        equalizeRepoCardHeights();
        scheduleFitContent();
      });
    });
  }

  function applyGridSettings(payload) {
    if (!payload) {
      return;
    }

    const columns = Number(payload.columns);
    const rows = Number(payload.rows);

    if (Number.isFinite(columns) && columns >= 1) {
      gridSettings.columns = columns;
    }

    if (Number.isFinite(rows) && rows >= 1) {
      gridSettings.rows = rows;
    }
  }

  function applyDisplaySettings(payload, options = {}) {
    if (!payload || typeof payload !== "object") {
      return;
    }

    const keys = [
      "description",
      "stats",
      "ci",
      "release",
      "heatmap",
      "feed",
      "pullRequests",
      "issues",
      "commits",
    ];

    for (const key of keys) {
      if (typeof payload[key] === "boolean") {
        displaySettings[key] = payload[key];
      }
    }

    if (options.silent) {
      return;
    }

    refreshAllRepoCards();
  }

  function computeLayout(viewport) {
    const configuredColumns = Math.max(1, gridSettings.columns);
    const configuredRows = Math.max(1, gridSettings.rows);
    const gridCapacity = configuredColumns * configuredRows;
    const occupiedCount = Object.keys(state.repos).length;

    let density = "default";
    if (viewport) {
      if (occupiedCount > gridCapacity) {
        density = "tight";
      } else if (occupiedCount >= 6 || gridCapacity >= 6) {
        density = occupiedCount >= 6 ? "tight" : "dense";
      } else if (occupiedCount >= 4) {
        density = "dense";
      } else if (occupiedCount >= 2) {
        density = "compact";
      }
    }

    const preset = layoutPresets[density];

    return {
      density,
      columns: configuredColumns,
      rows: configuredRows,
      commitLimit: preset.commitLimit,
    };
  }

  function getViewportSize() {
    if (lastViewport) {
      return lastViewport;
    }

    return {
      width: window.innerWidth,
      height: window.innerHeight,
      safeTop: 0,
      safeRight: 0,
      safeBottom: 0,
      safeLeft: 0,
    };
  }

  function getAvailableContentSize() {
    const wallpaper = wallpaperRoot;
    if (!wallpaper) {
      return {
        width: Math.max(1, window.innerWidth),
        height: Math.max(1, window.innerHeight),
      };
    }

    const wallpaperStyle = getComputedStyle(wallpaper);
    return {
      width: Math.max(
        1,
        window.innerWidth - readPx(wallpaperStyle.paddingLeft) - readPx(wallpaperStyle.paddingRight),
      ),
      height: Math.max(
        1,
        window.innerHeight - readPx(wallpaperStyle.paddingTop) - readPx(wallpaperStyle.paddingBottom),
      ),
    };
  }

  function measureWallpaperSize() {
    const target = wallpaperRoot || wallpaperContent || document.body;
    return {
      width: Math.max(target.scrollWidth, target.clientWidth),
      height: Math.max(target.scrollHeight, target.clientHeight),
    };
  }

  function forceReflow() {
    void document.body.offsetHeight;
  }

  function readPx(value) {
    const number = Number.parseFloat(value);
    return Number.isFinite(number) ? number : 0;
  }

  function scheduleFitContent() {
    for (const delay of FIT_RETRY_DELAYS_MS) {
      window.setTimeout(() => {
        if (delay === 0) {
          if (fitContentFrame) {
            cancelAnimationFrame(fitContentFrame);
          }

          fitContentFrame = requestAnimationFrame(() => {
            requestAnimationFrame(() => {
              fitContentFrame = 0;
              fitContentToViewport();
            });
          });
          return;
        }

        fitContentToViewport();
      }, delay);
    }
  }

  function fitContentToViewport() {
    if (isFittingContent) {
      return;
    }

    isFittingContent = true;
    document.body.style.zoom = "1";
    forceReflow();
    equalizeRepoCardHeights();
    forceReflow();
    fitCardContents();
    forceReflow();

    const { width: availWidth, height: availHeight } = getAvailableContentSize();
    const { width: naturalWidth, height: naturalHeight } = measureWallpaperSize();

    if (naturalWidth <= 0 || naturalHeight <= 0) {
      isFittingContent = false;
      return;
    }

    const zoom = Math.min(1, availWidth / naturalWidth, availHeight / naturalHeight);
    const rounded = Math.max(MIN_CONTENT_ZOOM, Math.round(zoom * 1000) / 1000);
    document.body.style.zoom = String(rounded);

    isFittingContent = false;
  }

  function ensureContentResizeObserver() {
    if (!wallpaperRoot || contentResizeObserver || typeof ResizeObserver === "undefined") {
      return;
    }

    contentResizeObserver = new ResizeObserver(() => {
      if (!isFittingContent) {
        scheduleFitContent();
      }
    });
    contentResizeObserver.observe(wallpaperRoot);
    if (repoGrid) {
      contentResizeObserver.observe(repoGrid);
    }
  }

  function applyLayout(layout) {
    const changed =
      layoutState.density !== layout.density ||
      layoutState.columns !== layout.columns ||
      layoutState.rows !== layout.rows ||
      layoutState.commitLimit !== layout.commitLimit;

    layoutState.density = layout.density;
    layoutState.columns = layout.columns;
    layoutState.rows = layout.rows;
    layoutState.commitLimit = layout.commitLimit;

    document.body.dataset.layout = layout.density;
    document.body.dataset.repoCount = String(Object.keys(state.repos).length);
    document.body.dataset.manyRepos =
      Object.keys(state.repos).length >= MANY_REPOS_THRESHOLD ? "true" : "false";
    document.body.dataset.gridColumns = String(layout.columns);
    document.body.dataset.gridRows = String(layout.rows);
    repoGrid.style.setProperty("--grid-columns", String(layout.columns));
    repoGrid.style.setProperty("--grid-rows", String(layout.rows));
    repoGrid.style.gridTemplateColumns = `repeat(${layout.columns}, minmax(0, 1fr))`;

    const stretchRows =
      Object.keys(state.repos).length >= MANY_REPOS_THRESHOLD || layout.columns * layout.rows >= 4;
    repoGrid.style.gridTemplateRows = stretchRows
      ? `repeat(${layout.rows}, minmax(0, 1fr))`
      : `repeat(${layout.rows}, minmax(0, auto))`;

    if (changed) {
      refreshAllRepoCards();
      return;
    }

    scheduleEqualizeAndFit();
  }

  function updateLayout(viewport) {
    if (viewport) {
      lastViewport = viewport;
    }

    const root = document.documentElement;
    const size = getViewportSize();
    const { width, height, safeTop, safeRight, safeBottom, safeLeft } = size;

    root.style.setProperty("--safe-top", `${safeTop}px`);
    root.style.setProperty("--safe-right", `${safeRight}px`);
    root.style.setProperty("--safe-bottom", `${safeBottom}px`);
    root.style.setProperty("--safe-left", `${safeLeft}px`);
    root.style.setProperty("--viewport-width", `${width}px`);
    root.style.setProperty("--viewport-height", `${height}px`);

    applyLayout(computeLayout(size));
  }

  function handleBridgeMessage(data) {
    if (!data || typeof data.type !== "string") {
      return;
    }

    switch (data.type) {
      case "pause":
        setPaused(true);
        return;
      case "resume":
        setPaused(false);
        return;
      case "auth:status":
        setAuthStatus(data.payload);
        dispatchBridgeEvent("wallpaper:auth-status", data);
        return;
      case "repos:init":
        if (data.layout) {
          applyGridSettings(data.layout);
        }
        if (data.display) {
          applyDisplaySettings(data.display, { silent: true });
        }
        initRepos(data.payload);
        dispatchBridgeEvent("wallpaper:repos-init", data);
        return;
      case "layout:update":
        applyGridSettings(data.payload);
        updateLayout(lastViewport);
        dispatchBridgeEvent("wallpaper:layout-update", data);
        return;
      case "display:update":
        applyDisplaySettings(data.payload);
        dispatchBridgeEvent("wallpaper:display-update", data);
        return;
      case "repo:metadata": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.metadata = data.payload;
        clearRepoError(entry);
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-metadata", data);
        return;
      }
      case "repo:commits": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.commits = data.payload;
        clearRepoError(entry);
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-commits", data);
        return;
      }
      case "repo:pulls": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.pulls = data.payload;
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-pulls", data);
        return;
      }
      case "repo:issues": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.issues = data.payload;
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-issues", data);
        return;
      }
      case "repo:releases": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.releases = data.payload;
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-releases", data);
        return;
      }
      case "repo:ci-run": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.ciRun = data.payload;
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-ci-run", data);
        return;
      }
      case "repo:heatmap": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.heatmap = data.payload;
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-heatmap", data);
        return;
      }
      case "repo:activity-feed": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.activityFeed = data.payload;
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-activity-feed", data);
        return;
      }
      case "repo:poll-failed": {
        const entry = ensureRepo(data.owner, data.repo);
        const silentKinds = new Set(["heatmap", "events"]);
        if (!silentKinds.has(data.payload?.kind)) {
          entry.lastError = data.payload;
        }
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-poll-failed", data);
        return;
      }
      case "viewport:update":
        updateLayout(data.payload);
        return;
      default:
        return;
    }
  }

  window.wallpaperBridge = {
    getState() {
      return state;
    },
    on(type, listener) {
      document.addEventListener(type, listener);
      return () => document.removeEventListener(type, listener);
    },
  };

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", (event) => {
      let data;

      try {
        data = typeof event.data === "string" ? JSON.parse(event.data) : event.data;
      } catch {
        return;
      }

      handleBridgeMessage(data);
    });
  }

  refreshAllRepoCards();
  ensureContentResizeObserver();
  window.addEventListener("resize", scheduleFitContent);
  scheduleFitContent();
})();

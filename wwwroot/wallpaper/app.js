(function () {
  const overlay = document.getElementById("pause-overlay");
  const repoGrid = document.getElementById("repo-grid");
  const authBanner = document.getElementById("auth-banner");

  const state = {
    repos: Object.create(null),
  };

  const cardElements = new Map();

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

  function openRepoUrl(url) {
    if (!url || !window.chrome?.webview) {
      return;
    }

    window.chrome.webview.postMessage(JSON.stringify({ type: "open-url", url }));
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

  function initRepos(repositories) {
    if (!Array.isArray(repositories)) {
      return;
    }

    const newKeys = new Set();
    const orderedEntries = [];

    for (const item of repositories) {
      if (!item?.owner || !item?.repo) {
        continue;
      }

      const key = repoKey(item.owner, item.repo);
      newKeys.add(key);
      orderedEntries.push(ensureRepo(item.owner, item.repo));
    }

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

    for (const entry of orderedEntries) {
      const key = repoKey(entry.owner, entry.repo);
      const card = cardElements.get(key);
      if (card) {
        repoGrid.appendChild(card);
      }
    }

    refreshAllRepoCards();
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
        const href = escapeHtml(item.htmlUrl || "#");
        return `<li><a href="${href}" target="_blank" rel="noopener noreferrer"><span class="repo-card__mini-number">${escapeHtml(number)}</span>${title}</a></li>`;
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
      .slice(0, 5)
      .map((commit) => {
        const sha = shortSha(commit.sha);
        const message = escapeHtml(commit.message || "Без сообщения");
        const author = escapeHtml(commit.authorName || "Неизвестный автор");
        const when = escapeHtml(formatRelativeDate(commit.authorDate));
        const href = escapeHtml(commit.htmlUrl || "#");

        return `
          <li class="repo-card__commit">
            <a class="repo-card__commit-sha" href="${href}" target="_blank" rel="noopener noreferrer">${sha}</a>
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
    const htmlUrl = metadata?.htmlUrl || `https://github.com/${key}`;
    const description = metadata?.description
      ? `<p class="repo-card__description">${escapeHtml(metadata.description)}</p>`
      : "";
    const stars = formatCount(metadata?.stars ?? 0);
    const forks = formatCount(metadata?.forks ?? 0);
    const pullCount = formatCount(entry.pulls?.length ?? 0);
    const issueCount = formatCount(entry.issues?.length ?? 0);
    const ciBadge = window.WallpaperCiBadge?.render(entry.ciRun) || "";
    const heatmap = window.WallpaperHeatmap?.render(entry.heatmap?.weeks) || "";
    const feed = window.WallpaperFeed?.render(entry.activityFeed) || "";
    const error = entry.lastError
      ? `<p class="repo-card__error" role="alert">${escapeHtml(formatPollError(entry.lastError))}</p>`
      : "";
    const loadingClass = metadata ? "" : " is-loading";
    const latestRelease = entry.releases?.[0];
    const releaseLine = latestRelease
      ? `<p class="repo-card__release">Последний релиз: <a href="${escapeHtml(latestRelease.htmlUrl || "#")}" target="_blank" rel="noopener noreferrer">${escapeHtml(latestRelease.name || latestRelease.tagName)}</a></p>`
      : "";

    return `
      <article class="repo-card${loadingClass}" data-repo-key="${escapeHtml(key)}" data-repo-url="${escapeHtml(htmlUrl)}" tabindex="0" role="link" aria-label="Открыть ${escapeHtml(fullName)} на GitHub">
        <header class="repo-card__header">
          <div class="repo-card__title-row">
            <h2 class="repo-card__title">
              <a href="${escapeHtml(htmlUrl)}" target="_blank" rel="noopener noreferrer">${escapeHtml(fullName)}</a>
            </h2>
            ${ciBadge}
          </div>
          ${description}
        </header>

        <div class="repo-card__stats">
          <span class="repo-card__stat" title="Звёзды">${icons.star}<span>${stars}</span></span>
          <span class="repo-card__stat" title="Форки">${icons.fork}<span>${forks}</span></span>
          <span class="repo-card__stat" title="Открытые PR">${icons.pr}<span>${pullCount}</span></span>
          <span class="repo-card__stat" title="Открытые issues">${icons.issue}<span>${issueCount}</span></span>
        </div>

        ${releaseLine}
        ${error}

        <section class="repo-card__heatmap">
          <h3 class="repo-card__section-title">Активность</h3>
          ${heatmap}
        </section>

        <section class="repo-card__feed">
          <h3 class="repo-card__section-title">Лента</h3>
          ${feed}
        </section>

        <div class="repo-card__columns">
          <section class="repo-card__mini-section">
            <h3 class="repo-card__section-title">Pull requests</h3>
            ${renderMiniList(entry.pulls, "Загрузка PR…")}
          </section>
          <section class="repo-card__mini-section">
            <h3 class="repo-card__section-title">Issues</h3>
            ${renderMiniList(entry.issues, "Загрузка issues…")}
          </section>
        </div>

        <section class="repo-card__commits">
          <h3 class="repo-card__section-title">Последние коммиты</h3>
          ${renderCommits(entry)}
        </section>
      </article>
    `;
  }

  function bindRepoCardInteractions(card, entry) {
    const htmlUrl = entry.metadata?.htmlUrl || `https://github.com/${repoKey(entry.owner, entry.repo)}`;

    card.onclick = (event) => {
      if (event.target.closest("a")) {
        return;
      }

      openRepoUrl(htmlUrl);
    };

    card.onkeydown = (event) => {
      if (event.key !== "Enter" && event.key !== " ") {
        return;
      }

      event.preventDefault();
      openRepoUrl(htmlUrl);
    };
  }

  function mountRepoCard(entry) {
    const key = repoKey(entry.owner, entry.repo);
    let card = cardElements.get(key);

    if (!card) {
      card = document.createElement("div");
      cardElements.set(key, card);
      repoGrid.appendChild(card);
    }

    card.innerHTML = renderRepoCard(entry);
    bindRepoCardInteractions(card.firstElementChild, entry);

    const feedSection = card.querySelector(".repo-card__feed");
    window.WallpaperFeed?.markNewItemsAnimated(feedSection);
  }

  function refreshRepoCard(owner, repo) {
    const entry = state.repos[repoKey(owner, repo)];
    if (!entry) {
      return;
    }

    mountRepoCard(entry);
  }

  function refreshAllRepoCards() {
    for (const entry of Object.values(state.repos)) {
      mountRepoCard(entry);
    }
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
        initRepos(data.payload);
        dispatchBridgeEvent("wallpaper:repos-init", data);
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
        entry.lastError = data.payload;
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-poll-failed", data);
        return;
      }
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
})();

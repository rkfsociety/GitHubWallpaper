(function () {
  const overlay = document.getElementById("pause-overlay");
  const repoGrid = document.getElementById("repo-grid");

  const state = {
    repos: Object.create(null),
  };

  const cardElements = new Map();

  const icons = {
    star:
      '<svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M8 .25a.75.75 0 0 1 .673.418l1.882 3.815 4.21.612a.75.75 0 0 1 .416 1.279l-3.046 2.97.719 4.192a.751.751 0 0 1-1.088.791L8 12.347l-3.766 1.98a.75.75 0 0 1-1.088-.79l.72-4.194L.818 6.374a.75.75 0 0 1 .416-1.28l4.21-.611L7.327.668A.75.75 0 0 1 8 .25Z"/></svg>',
    fork:
      '<svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M5 5.372v.878c0 .414.336.75.75.75h4.5a.75.75 0 0 0 .75-.75v-.878a2.25 2.25 0 1 1 1.5 0v.878a2.25 2.25 0 0 1-2.25 2.25h-1.5v2.128a2.251 2.251 0 1 1-1.5 0V8.5h-1.5A2.25 2.25 0 0 1 3.5 6.25v-.878a2.25 2.25 0 1 1 1.5 0ZM5 3.25a.75.75 0 1 0-1.5 0 .75.75 0 0 0 1.5 0Zm6.75.75a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm-3 8.75a.75.75 0 1 0-1.5 0 .75.75 0 0 0 1.5 0Z"/></svg>',
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

  function dispatchBridgeEvent(name, detail) {
    document.dispatchEvent(new CustomEvent(name, { detail }));
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
    const error = entry.lastError
      ? `<p class="repo-card__error">${escapeHtml(entry.lastError.message || "Ошибка опроса")}</p>`
      : "";
    const loadingClass = metadata ? "" : " is-loading";

    return `
      <article class="repo-card${loadingClass}" data-repo-key="${escapeHtml(key)}">
        <header class="repo-card__header">
          <h2 class="repo-card__title">
            <a href="${escapeHtml(htmlUrl)}" target="_blank" rel="noopener noreferrer">${escapeHtml(fullName)}</a>
          </h2>
          ${description}
        </header>

        <div class="repo-card__stats">
          <span class="repo-card__stat" title="Звёзды">${icons.star}<span>${stars}</span></span>
          <span class="repo-card__stat" title="Форки">${icons.fork}<span>${forks}</span></span>
        </div>

        ${error}

        <section class="repo-card__commits">
          <h3 class="repo-card__commits-title">Последние коммиты</h3>
          ${renderCommits(entry)}
        </section>
      </article>
    `;
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
      case "repo:metadata": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.metadata = data.payload;
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-metadata", data);
        return;
      }
      case "repo:commits": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.commits = data.payload;
        refreshRepoCard(data.owner, data.repo);
        dispatchBridgeEvent("wallpaper:repo-commits", data);
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

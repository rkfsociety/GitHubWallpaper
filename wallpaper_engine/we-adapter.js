(function () {
  "use strict";

  const GITHUB_API = "https://api.github.com";

  const state = {
    token: "",
    refreshSeconds: 120,
    columns: 3,
    rows: 2,
    repos: [],
    display: {
      description: true,
      stats: true,
      ci: true,
      release: true,
      heatmap: true,
      feed: true,
      pullRequests: true,
      issues: true,
      commits: true,
    },
  };

  const cache = {
    lastRunAt: 0,
    repoData: new Map(), // key -> { commits, pulls, issues, releases, ciRun, heatmapWeeks, feed }
    heatmapPendingUntil: new Map(), // key -> timestamp
  };

  let timerId = 0;
  let pollInFlight = false;
  let pageReady = false;

  function nowIso() {
    return new Date().toISOString();
  }

  function gridCapacity() {
    return Math.max(1, state.columns) * Math.max(1, state.rows);
  }

  function readRepoSlots(properties) {
    const capacity = gridCapacity();
    const repos = [];
    for (let i = 1; i <= capacity; i += 1) {
      const key = `repo${i}`;
      if (properties?.[key]) {
        repos.push(parseRepoSlug(properties[key].value));
        continue;
      }
      if (state.repos[i - 1]) {
        repos.push(state.repos[i - 1]);
        continue;
      }
      repos.push(null);
    }
    return repos;
  }

  function repoKey(owner, repo) {
    return `${owner}/${repo}`;
  }

  function parseRepoSlug(value) {
    const text = String(value || "").trim();
    if (!text) {
      return null;
    }
    const normalized = text.replace(/^https?:\/\/github\.com\//i, "").replace(/\/+$/, "");
    const parts = normalized.split("/");
    if (parts.length < 2) {
      return null;
    }
    const owner = parts[0].trim();
    const repo = parts[1].trim();
    if (!owner || !repo) {
      return null;
    }
    return { owner, repo };
  }

  function dispatchToApp(message) {
    if (!window.chrome?.webview?.dispatchMessage) {
      return;
    }
    try {
      window.chrome.webview.dispatchMessage(JSON.stringify(message));
    } catch {
      // ignore
    }
  }

  function pushAuthStatus() {
    const hasToken = Boolean(state.token && state.token.trim());
    dispatchToApp({
      type: "auth:status",
      payload: {
        hasToken,
        rateLimit: null,
        rateLimitRemaining: null,
        message: hasToken
          ? null
          : "GitHub token не задан — лимит 60 запросов/час. Добавьте PAT в настройках Wallpaper Engine.",
      },
    });
  }

  function pushReposInit() {
    const capacity = gridCapacity();
    const slots = [];
    for (let i = 0; i < capacity; i += 1) {
      const repo = state.repos[i];
      if (!repo) {
        slots.push(null);
      } else {
        slots.push({ owner: repo.owner, repo: repo.repo });
      }
    }

    dispatchToApp({
      type: "repos:init",
      payload: slots,
      layout: { columns: state.columns, rows: state.rows },
      display: { ...state.display },
    });
  }

  function pushDisplayUpdate() {
    dispatchToApp({ type: "display:update", payload: { ...state.display } });
  }

  function pushLayoutUpdate() {
    dispatchToApp({ type: "layout:update", payload: { columns: state.columns, rows: state.rows } });
  }

  function toPollError(kind, status, message, hint) {
    let code = "unknown";
    if (status === 401) code = "unauthorized";
    else if (status === 403) code = "forbidden";
    else if (status === 404) code = "not_found";
    else if (status === 429) code = "rate_limited";
    else if (status >= 500) code = "server_error";
    return {
      kind,
      code,
      message: message || "Ошибка GitHub API",
      hint: hint || null,
    };
  }

  async function githubFetchJson(path, { accept, etag } = {}) {
    const headers = {
      Accept: accept || "application/vnd.github+json",
      "X-GitHub-Api-Version": "2022-11-28",
    };
    if (state.token) {
      headers.Authorization = `Bearer ${state.token}`;
    }
    if (etag) {
      headers["If-None-Match"] = etag;
    }

    const response = await fetch(`${GITHUB_API}${path}`, {
      method: "GET",
      headers,
      cache: "no-store",
    });

    if (response.status === 304) {
      return { status: 304, etag: etag || null, json: null, text: null };
    }

    const newEtag = response.headers.get("etag");
    const text = await response.text();
    let json = null;
    try {
      json = text ? JSON.parse(text) : null;
    } catch {
      json = null;
    }

    return { status: response.status, etag: newEtag, json, text };
  }

  function listFrom(value) {
    return Array.isArray(value) ? value : [];
  }

  function buildMetadataPayload(owner, repo, json) {
    return {
      type: "repo:metadata",
      owner,
      repo,
      payload: {
        fullName: json?.full_name || `${owner}/${repo}`,
        description: json?.description ?? null,
        stars: Number(json?.stargazers_count) || 0,
        forks: Number(json?.forks_count) || 0,
        openIssues: Number(json?.open_issues_count) || 0,
        htmlUrl: json?.html_url || `https://github.com/${owner}/${repo}`,
        fetchedAt: nowIso(),
      },
    };
  }

  function buildCommitsPayload(owner, repo, json) {
    const commits = listFrom(json).map((item) => {
      const message = String(item?.commit?.message || "").split("\n", 1)[0];
      return {
        sha: item?.sha || "",
        message,
        authorName: item?.commit?.author?.name ?? null,
        authorDate: item?.commit?.author?.date ?? null,
        htmlUrl: item?.html_url || "",
      };
    });
    return { type: "repo:commits", owner, repo, payload: commits };
  }

  function buildPullsPayload(owner, repo, json) {
    const pulls = listFrom(json).map((item) => ({
      number: Number(item?.number) || 0,
      title: item?.title || "",
      userLogin: item?.user?.login ?? null,
      createdAt: item?.created_at ?? null,
      htmlUrl: item?.html_url || "",
    }));
    return { type: "repo:pulls", owner, repo, payload: pulls };
  }

  function buildIssuesPayload(owner, repo, json) {
    const issues = listFrom(json)
      .filter((item) => !item?.pull_request)
      .map((item) => ({
        number: Number(item?.number) || 0,
        title: item?.title || "",
        userLogin: item?.user?.login ?? null,
        createdAt: item?.created_at ?? null,
        htmlUrl: item?.html_url || "",
      }));
    return { type: "repo:issues", owner, repo, payload: issues };
  }

  function buildReleasesPayload(owner, repo, json) {
    const releases = listFrom(json).map((item) => ({
      id: Number(item?.id) || 0,
      tagName: item?.tag_name || "",
      name: item?.name || item?.tag_name || "",
      isPrerelease: Boolean(item?.prerelease),
      publishedAt: item?.published_at ?? null,
      htmlUrl: item?.html_url || "",
    }));
    return { type: "repo:releases", owner, repo, payload: releases };
  }

  function buildCiRunPayload(owner, repo, json) {
    const run = listFrom(json?.workflow_runs)[0];
    const payload = run
      ? {
          id: Number(run.id) || 0,
          name: run.name || "Workflow",
          status: run.status || "unknown",
          conclusion: run.conclusion ?? null,
          updatedAt: run.updated_at ?? null,
          htmlUrl: run.html_url || "",
        }
      : null;
    return { type: "repo:ci-run", owner, repo, payload };
  }

  function buildHeatmapPayload(owner, repo, json) {
    const weeks = listFrom(json).map((week) => {
      const days = listFrom(week?.days).slice(0, 7).map((d) => (Number.isFinite(Number(d)) ? Number(d) : 0));
      while (days.length < 7) days.push(0);
      return { total: Number(week?.total) || 0, days };
    });
    return { type: "repo:heatmap", owner, repo, payload: { weeks, fetchedAt: nowIso() } };
  }

  function storageKey(repo) {
    return `ghw_we_last_seen:${repo.owner}/${repo.repo}`;
  }

  function markNewFeedItems(repo, items) {
    const key = storageKey(repo);
    const lastSeen = Number(localStorage.getItem(key) || "0") || 0;
    let maxTs = lastSeen;
    const marked = items.map((item) => {
      const ts = new Date(item.timestamp).getTime();
      const safeTs = Number.isFinite(ts) ? ts : 0;
      if (safeTs > maxTs) maxTs = safeTs;
      return { ...item, isNew: safeTs > lastSeen && lastSeen > 0 };
    });
    localStorage.setItem(key, String(maxTs || Date.now()));
    return marked;
  }

  function buildActivityFeed(repo, repoData) {
    const items = [];

    for (const c of repoData.commits || []) {
      items.push({
        id: `commit:${c.sha}`,
        kind: "push",
        title: c.message || "Commit",
        subtitle: c.authorName || null,
        timestamp: c.authorDate || nowIso(),
        htmlUrl: c.htmlUrl || `https://github.com/${repo.owner}/${repo.repo}/commit/${c.sha}`,
        isNew: false,
      });
    }

    for (const pr of repoData.pulls || []) {
      items.push({
        id: `pr:${pr.number}`,
        kind: "pr",
        title: pr.title || `PR #${pr.number}`,
        subtitle: pr.userLogin ? `@${pr.userLogin}` : null,
        timestamp: pr.createdAt || nowIso(),
        htmlUrl: pr.htmlUrl || `https://github.com/${repo.owner}/${repo.repo}/pull/${pr.number}`,
        isNew: false,
      });
    }

    for (const issue of repoData.issues || []) {
      items.push({
        id: `issue:${issue.number}`,
        kind: "issue",
        title: issue.title || `Issue #${issue.number}`,
        subtitle: issue.userLogin ? `@${issue.userLogin}` : null,
        timestamp: issue.createdAt || nowIso(),
        htmlUrl: issue.htmlUrl || `https://github.com/${repo.owner}/${repo.repo}/issues/${issue.number}`,
        isNew: false,
      });
    }

    const rel = repoData.releases?.[0];
    if (rel) {
      items.push({
        id: `release:${rel.id || rel.tagName}`,
        kind: "release",
        title: rel.name || rel.tagName || "Release",
        subtitle: rel.isPrerelease ? "prerelease" : null,
        timestamp: rel.publishedAt || nowIso(),
        htmlUrl: rel.htmlUrl || `https://github.com/${repo.owner}/${repo.repo}/releases`,
        isNew: false,
      });
    }

    if (repoData.ciRun) {
      items.push({
        id: `ci:${repoData.ciRun.id || repoData.ciRun.updatedAt || "latest"}`,
        kind: "ci",
        title: repoData.ciRun.name || "CI",
        subtitle: repoData.ciRun.conclusion || repoData.ciRun.status || null,
        timestamp: repoData.ciRun.updatedAt || nowIso(),
        htmlUrl: repoData.ciRun.htmlUrl || `https://github.com/${repo.owner}/${repo.repo}/actions`,
        isNew: false,
      });
    }

    items.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());
    return markNewFeedItems(repo, items.slice(0, 18));
  }

  async function pollRepo(repo) {
    const key = repoKey(repo.owner, repo.repo);
    const data = cache.repoData.get(key) || {};

    try {
      // Metadata
      const meta = await githubFetchJson(`/repos/${repo.owner}/${repo.repo}`);
      if (meta.status >= 200 && meta.status < 300) {
        dispatchToApp(buildMetadataPayload(repo.owner, repo.repo, meta.json));
      } else {
        dispatchToApp({
          type: "repo:poll-failed",
          owner: repo.owner,
          repo: repo.repo,
          payload: toPollError("metadata", meta.status, meta.text),
        });
      }

      // Commits
      if (state.display.commits || state.display.feed) {
        const commits = await githubFetchJson(`/repos/${repo.owner}/${repo.repo}/commits?per_page=5`);
        if (commits.status >= 200 && commits.status < 300) {
          const payload = buildCommitsPayload(repo.owner, repo.repo, commits.json);
          data.commits = payload.payload;
          dispatchToApp(payload);
        } else {
          dispatchToApp({
            type: "repo:poll-failed",
            owner: repo.owner,
            repo: repo.repo,
            payload: toPollError("commits", commits.status, commits.text),
          });
        }
      }

      // Pulls
      if (state.display.pullRequests || state.display.feed) {
        const pulls = await githubFetchJson(`/repos/${repo.owner}/${repo.repo}/pulls?state=open&per_page=10`);
        if (pulls.status >= 200 && pulls.status < 300) {
          const payload = buildPullsPayload(repo.owner, repo.repo, pulls.json);
          data.pulls = payload.payload;
          dispatchToApp(payload);
        } else {
          dispatchToApp({
            type: "repo:poll-failed",
            owner: repo.owner,
            repo: repo.repo,
            payload: toPollError("pullrequests", pulls.status, pulls.text),
          });
        }
      }

      // Issues
      if (state.display.issues || state.display.feed) {
        const issues = await githubFetchJson(`/repos/${repo.owner}/${repo.repo}/issues?state=open&per_page=10`);
        if (issues.status >= 200 && issues.status < 300) {
          const payload = buildIssuesPayload(repo.owner, repo.repo, issues.json);
          data.issues = payload.payload;
          dispatchToApp(payload);
        } else {
          dispatchToApp({
            type: "repo:poll-failed",
            owner: repo.owner,
            repo: repo.repo,
            payload: toPollError("issues", issues.status, issues.text),
          });
        }
      }

      // Releases
      if (state.display.release || state.display.feed) {
        const releases = await githubFetchJson(`/repos/${repo.owner}/${repo.repo}/releases?per_page=5`);
        if (releases.status >= 200 && releases.status < 300) {
          const payload = buildReleasesPayload(repo.owner, repo.repo, releases.json);
          data.releases = payload.payload;
          dispatchToApp(payload);
        } else {
          dispatchToApp({
            type: "repo:poll-failed",
            owner: repo.owner,
            repo: repo.repo,
            payload: toPollError("releases", releases.status, releases.text),
          });
        }
      }

      // CI run
      if (state.display.ci || state.display.feed) {
        const ci = await githubFetchJson(`/repos/${repo.owner}/${repo.repo}/actions/runs?per_page=1`);
        if (ci.status >= 200 && ci.status < 300) {
          const payload = buildCiRunPayload(repo.owner, repo.repo, ci.json);
          data.ciRun = payload.payload;
          dispatchToApp(payload);
        } else {
          dispatchToApp({
            type: "repo:poll-failed",
            owner: repo.owner,
            repo: repo.repo,
            payload: toPollError("cirun", ci.status, ci.text),
          });
        }
      }

      // Heatmap (GitHub /stats is special: might return 202 while calculating)
      if (state.display.heatmap) {
        const pendingUntil = cache.heatmapPendingUntil.get(key) || 0;
        if (Date.now() >= pendingUntil) {
          const heatmap = await githubFetchJson(`/repos/${repo.owner}/${repo.repo}/stats/commit_activity`);
          if (heatmap.status === 202) {
            cache.heatmapPendingUntil.set(key, Date.now() + 2 * 60_000);
          } else if (heatmap.status >= 200 && heatmap.status < 300) {
            const payload = buildHeatmapPayload(repo.owner, repo.repo, heatmap.json);
            data.heatmap = payload.payload;
            dispatchToApp(payload);
          }
        }
      }

      // Activity feed (derived)
      if (state.display.feed) {
        const feed = buildActivityFeed(repo, data);
        data.feed = feed;
        dispatchToApp({ type: "repo:activity-feed", owner: repo.owner, repo: repo.repo, payload: feed });
      }

      cache.repoData.set(key, data);
    } catch (err) {
      dispatchToApp({
        type: "repo:poll-failed",
        owner: repo.owner,
        repo: repo.repo,
        payload: {
          kind: "metadata",
          code: "network",
          message: String(err?.message || err || "Ошибка сети"),
          hint: null,
        },
      });
    }
  }

  async function runPoll() {
    if (pollInFlight) {
      return;
    }
    pollInFlight = true;
    try {
      for (const repo of state.repos) {
        if (!repo) continue;
        // eslint-disable-next-line no-await-in-loop
        await pollRepo(repo);
      }
      cache.lastRunAt = Date.now();
    } finally {
      pollInFlight = false;
    }
  }

  function restartTimer() {
    if (timerId) {
      clearInterval(timerId);
      timerId = 0;
    }
    const intervalMs = Math.max(30, Number(state.refreshSeconds) || 120) * 1000;
    timerId = setInterval(() => {
      if (!pageReady) return;
      runPoll();
    }, intervalMs);
  }

  function applyAllSettings() {
    pushAuthStatus();
    pushLayoutUpdate();
    pushDisplayUpdate();
    pushReposInit();
    restartTimer();
    if (pageReady) {
      runPoll();
    }
  }

  function applyProperties(properties) {
    if (!properties || typeof properties !== "object") {
      return;
    }

    if (properties.githubToken) {
      state.token = String(properties.githubToken.value || "").trim();
    }
    if (properties.refreshSeconds) {
      state.refreshSeconds = Number(properties.refreshSeconds.value) || 120;
    }
    if (properties.columns) {
      state.columns = Math.max(1, Math.min(6, Number(properties.columns.value) || 3));
    }
    if (properties.rows) {
      state.rows = Math.max(1, Math.min(4, Number(properties.rows.value) || 2));
    }

    state.repos = readRepoSlots(properties);

    const bools = [
      ["showDescription", "description"],
      ["showStats", "stats"],
      ["showCi", "ci"],
      ["showRelease", "release"],
      ["showHeatmap", "heatmap"],
      ["showFeed", "feed"],
      ["showPullRequests", "pullRequests"],
      ["showIssues", "issues"],
      ["showCommits", "commits"],
    ];
    for (const [propKey, displayKey] of bools) {
      if (properties[propKey]) {
        state.display[displayKey] = Boolean(properties[propKey].value);
      }
    }

    applyAllSettings();
  }

  function installPropertyListener() {
    window.wallpaperPropertyListener = window.wallpaperPropertyListener || {};
    window.wallpaperPropertyListener.applyUserProperties = function (properties) {
      applyProperties(properties);
    };

    // Некоторые события WE (если доступны)
    window.wallpaperPropertyListener.setPaused = function (isPaused) {
      dispatchToApp({ type: isPaused ? "pause" : "resume" });
    };
  }

  function hookVisibilityPause() {
    document.addEventListener("visibilitychange", () => {
      dispatchToApp({ type: document.hidden ? "pause" : "resume" });
    });
  }

  function hookPageReadyFromApp() {
    // app.js шлёт `page:ready` через webview.postMessage. Мы это не перехватываем надёжно,
    // поэтому считаем страницу готовой после DOMContentLoaded + небольшой задержки.
    pageReady = true;
    applyAllSettings();
  }

  function bootstrapDefaultsIfNoWE() {
    // Если запущено в обычном браузере, просто стартуем с дефолтами.
    if (!("wallpaperPropertyListener" in window)) {
      state.repos = [parseRepoSlug("microsoft/vscode"), null, null, null, null, null];
      applyAllSettings();
    }
  }

  installPropertyListener();
  hookVisibilityPause();

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => {
      hookPageReadyFromApp();
      bootstrapDefaultsIfNoWE();
    });
  } else {
    hookPageReadyFromApp();
    bootstrapDefaultsIfNoWE();
  }
})();

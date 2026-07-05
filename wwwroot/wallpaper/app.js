(function () {
  const overlay = document.getElementById("pause-overlay");

  const state = {
    repos: Object.create(null),
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

  function setPaused(paused) {
    document.body.classList.toggle("is-paused", paused);
    overlay.hidden = !paused;
  }

  function dispatchBridgeEvent(name, detail) {
    document.dispatchEvent(new CustomEvent(name, { detail }));
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
        dispatchBridgeEvent("wallpaper:repo-metadata", data);
        return;
      }
      case "repo:commits": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.commits = data.payload;
        dispatchBridgeEvent("wallpaper:repo-commits", data);
        return;
      }
      case "repo:poll-failed": {
        const entry = ensureRepo(data.owner, data.repo);
        entry.lastError = data.payload;
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
})();

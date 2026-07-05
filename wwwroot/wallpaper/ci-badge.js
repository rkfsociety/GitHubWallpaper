(function (global) {
  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function resolveStatus(run) {
    if (!run) {
      return {
        label: "CI",
        className: "ci-badge--unknown",
        title: "Нет данных CI",
      };
    }

    const conclusion = String(run.conclusion || "").toLowerCase();
    const status = String(run.status || "").toLowerCase();

    if (conclusion === "success") {
      return {
        label: "CI ✓",
        className: "ci-badge--success",
        title: `${run.name || "Workflow"}: успешно`,
      };
    }

    if (conclusion === "failure") {
      return {
        label: "CI ✕",
        className: "ci-badge--failure",
        title: `${run.name || "Workflow"}: ошибка`,
      };
    }

    if (conclusion === "cancelled") {
      return {
        label: "CI —",
        className: "ci-badge--cancelled",
        title: `${run.name || "Workflow"}: отменён`,
      };
    }

    if (status === "in_progress" || status === "queued" || status === "waiting") {
      return {
        label: "CI …",
        className: "ci-badge--pending",
        title: `${run.name || "Workflow"}: выполняется`,
      };
    }

    return {
      label: "CI ?",
      className: "ci-badge--unknown",
      title: `${run.name || "Workflow"}: ${conclusion || status || "неизвестно"}`,
    };
  }

  function renderBadge(run) {
    const status = resolveStatus(run);
    const href = run?.htmlUrl ? escapeHtml(run.htmlUrl) : "#";

    return `
      <a class="ci-badge ${status.className}" href="${href}" target="_blank" rel="noopener noreferrer" title="${escapeHtml(status.title)}">
        <span class="ci-badge__dot" aria-hidden="true"></span>
        <span class="ci-badge__label">${escapeHtml(status.label)}</span>
      </a>
    `;
  }

  global.WallpaperCiBadge = {
    render: renderBadge,
    resolveStatus,
  };
})(window);

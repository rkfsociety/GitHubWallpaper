(function (global) {
  const kindLabels = {
    push: "Push",
    pr: "PR",
    issue: "Issue",
    release: "Release",
    ci: "CI",
    watch: "Star",
    fork: "Fork",
    event: "Event",
  };

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
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
        const amount = Math.round(diffMinutes / range.divisor) || 0;
        return formatter.format(amount, range.unit);
      }
    }

    return formatter.format(Math.round(diffMinutes / (365 * 24 * 60)), "year");
  }

  function renderFeed(items) {
    if (!Array.isArray(items)) {
      return '<p class="repo-card__placeholder">Загрузка ленты…</p>';
    }

    if (items.length === 0) {
      return '<p class="repo-card__placeholder">Событий пока нет</p>';
    }

    const rows = items
      .slice(0, 12)
      .map((item) => {
        const kind = escapeHtml(kindLabels[item.kind] || item.kind || "Event");
        const title = escapeHtml(item.title || "Событие");
        const subtitle = item.subtitle ? `<span class="feed__subtitle">${escapeHtml(item.subtitle)}</span>` : "";
        const when = escapeHtml(formatRelativeDate(item.timestamp));
        const newClass = item.isNew ? " feed__item--new" : "";

        return `
          <li class="feed__item${newClass}" data-feed-id="${escapeHtml(item.id || "")}">
            <div class="feed__row">
              <span class="feed__kind">${kind}</span>
              <span class="feed__title" title="${title}">${title}</span>
              ${subtitle}
              <span class="feed__time">${when}</span>
            </div>
          </li>
        `;
      })
      .join("");

    return `<ul class="feed__list">${rows}</ul>`;
  }

  function markNewItemsAnimated(container) {
    if (!container) {
      return;
    }

    const newItems = container.querySelectorAll(".feed__item--new");
    for (const item of newItems) {
      item.addEventListener(
        "animationend",
        () => {
          item.classList.remove("feed__item--new");
        },
        { once: true },
      );
    }
  }

  global.WallpaperFeed = {
    render: renderFeed,
    markNewItemsAnimated,
  };
})(window);

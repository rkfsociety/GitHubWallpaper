(function (global) {
  const levelColors = [
    "var(--heatmap-0)",
    "var(--heatmap-1)",
    "var(--heatmap-2)",
    "var(--heatmap-3)",
    "var(--heatmap-4)",
  ];

  function maxDayCount(weeks) {
    let max = 0;
    for (const week of weeks || []) {
      for (const count of week.days || []) {
        if (count > max) {
          max = count;
        }
      }
    }
    return max || 1;
  }

  function levelForCount(count, max) {
    if (!count) {
      return 0;
    }

    const ratio = count / max;
    if (ratio >= 0.75) {
      return 4;
    }

    if (ratio >= 0.5) {
      return 3;
    }

    if (ratio >= 0.25) {
      return 2;
    }

    return 1;
  }

  function renderHeatmap(weeks) {
    const normalizedWeeks = Array.isArray(weeks) ? weeks.slice(-52) : [];

    if (normalizedWeeks.length === 0) {
      return '<p class="repo-card__placeholder">Загрузка heatmap…</p>';
    }

    const max = maxDayCount(normalizedWeeks);
    const columns = normalizedWeeks
      .map((week) => {
        const days = (week.days || []).slice(0, 7);
        while (days.length < 7) {
          days.push(0);
        }

        const cells = days
          .map((count) => {
            const level = levelForCount(count, max);
            return `<span class="heatmap__cell" data-level="${level}" style="background:${levelColors[level]}" title="${count} коммитов"></span>`;
          })
          .join("");

        return `<div class="heatmap__week" aria-hidden="true">${cells}</div>`;
      })
      .join("");

    return `
      <div class="heatmap" role="img" aria-label="Активность коммитов за 52 недели">
        <div class="heatmap__grid">${columns}</div>
        <div class="heatmap__legend" aria-hidden="true">
          <span>Меньше</span>
          ${levelColors.map((color, index) => `<span class="heatmap__legend-cell" style="background:${color}" data-level="${index}"></span>`).join("")}
          <span>Больше</span>
        </div>
      </div>
    `;
  }

  global.WallpaperHeatmap = {
    render: renderHeatmap,
  };
})(window);

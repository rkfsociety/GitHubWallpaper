(function () {
  const overlay = document.getElementById("pause-overlay");

  function setPaused(paused) {
    document.body.classList.toggle("is-paused", paused);
    overlay.hidden = !paused;
  }

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", (event) => {
      let data;

      try {
        data = typeof event.data === "string" ? JSON.parse(event.data) : event.data;
      } catch {
        return;
      }

      if (data.type === "pause") {
        setPaused(true);
      } else if (data.type === "resume") {
        setPaused(false);
      }
    });
  }
})();

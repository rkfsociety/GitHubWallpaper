(function () {
  "use strict";

  const messageListeners = [];

  function dispatchMessage(data) {
    for (const listener of messageListeners) {
      try {
        listener({ data });
      } catch {
        // ignore listener errors
      }
    }
  }

  const webview = {
    addEventListener(type, listener) {
      if (type === "message" && typeof listener === "function") {
        messageListeners.push(listener);
      }
    },
    removeEventListener(type, listener) {
      if (type !== "message") {
        return;
      }
      const index = messageListeners.indexOf(listener);
      if (index >= 0) {
        messageListeners.splice(index, 1);
      }
    },
    dispatchMessage(jsonOrObj) {
      let payload = jsonOrObj;
      if (typeof jsonOrObj === "string") {
        try {
          payload = JSON.parse(jsonOrObj);
        } catch {
          payload = jsonOrObj;
        }
      }
      dispatchMessage(payload);
    },
    postMessage(message) {
      // app.js использует это для `page:ready` и `open-url`.
      let data = message;
      if (typeof message === "string") {
        try {
          data = JSON.parse(message);
        } catch {
          return;
        }
      }

      if (!data || typeof data.type !== "string") {
        return;
      }

      if (data.type === "open-url" && typeof data.url === "string" && data.url) {
        try {
          window.open(data.url, "_blank");
        } catch {
          // ignore
        }
      }
    },
  };

  window.chrome = window.chrome || {};
  window.chrome.webview = window.chrome.webview || webview;
})();

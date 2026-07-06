// Shim window.chrome.webview для Qt WebEngine (QWebChannel + runJavaScript).
(function () {
  "use strict";

  const messageListeners = [];

  function dispatchMessage(jsonStr) {
    let data;
    try {
      data = typeof jsonStr === "string" ? JSON.parse(jsonStr) : jsonStr;
    } catch {
      return;
    }

    for (const listener of messageListeners) {
      listener({ data });
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
    postMessage(message) {
      const payload = typeof message === "string" ? message : JSON.stringify(message);
      if (window.bridgeHost && typeof window.bridgeHost.receiveFromJs === "function") {
        window.bridgeHost.receiveFromJs(payload);
      }
    },
    dispatchMessage,
  };

  window.chrome = window.chrome || {};
  window.chrome.webview = webview;

  function onChannelReady() {
    webview.postMessage(JSON.stringify({ type: "page:ready" }));
  }

  function connectChannel() {
    if (typeof QWebChannel === "undefined" || !window.qt || !window.qt.webChannelTransport) {
      return false;
    }

    new QWebChannel(window.qt.webChannelTransport, function (channel) {
      window.bridgeHost = channel.objects.bridgeHost;
      onChannelReady();
    });
    return true;
  }

  if (!connectChannel()) {
    const poll = setInterval(function () {
      if (connectChannel()) {
        clearInterval(poll);
      }
    }, 10);
    setTimeout(function () {
      clearInterval(poll);
    }, 5000);
  }
})();

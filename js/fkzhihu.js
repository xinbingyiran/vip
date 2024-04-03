// ==UserScript==
// @name         fkzhihu
// @namespace    http://tampermonkey.net/
// @version      1.0.0
// @description  知乎登录弹窗去除
// @author       x
// @match        *://*.zhihu.com/*
// @grant        none
// @icon         https://static.zhihu.com/heifetz/favicon.ico
// @run-at       document-end
// ==/UserScript==

!function () {
  'use strict';

  const config = { attributes: false, childList: true, subtree: true };

  const callback = function (mutationsList, observer) {
      document.querySelector(".Modal-closeButton")?.click();
      mutationsList.some(m=>m.addedNodes && m.addedNodes.length) && [].map.call(document.querySelectorAll('a'), s => s).forEach(a=>a.href && a.href.startsWith("https://link.zhihu.com/?target=http") && (a.href = unescape(a.href.substr("https://link.zhihu.com/?target=".length))))
  };

  const observer = new MutationObserver(callback);
  observer.observe(document, config);

}();
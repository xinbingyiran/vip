// ==UserScript==
// @name         fkcsdn
// @namespace    http://tampermonkey.net/
// @version      1.0.0
// @description  csdn变好用
// @author       x
// @match        *://blog.csdn.net/*
// @grant        none
// @icon         https://g.csdnimg.cn/static/logo/favicon32.ico
// @run-at       document-end
// ==/UserScript==

!function () {
  'use strict';
  const each = (s, a) => {
    s.forEach ? s.forEach(a) : [].map.call(s, a)
  }
  const unbindall = (s) => {
    if (window.$ && window.$._data) {
      const events = window.$._data(s, "events");
      events && Object.keys(events).forEach(s => delete events[s]);
    }
  }
  const tick = () => {
    if (window.csdn && window.csdn.copyright && window.csdn.copyright.textData != "") {
      window.csdn.copyright.textData = '';
    }
    if (window.$ && window.$._data) {
      each(window.$("#content_views"), unbindall);
      each(window.$("div.blog-content-box"), unbindall);
      window.$("pre").attr("contentEditable", true);
      if (window.$(".look-more-preCode,.contentImg-no-view").length) {
        window.$(".look-more-preCode,.contentImg-no-view").click();
      }
      if (window.$(".hljs-button,.signin").length) {
        window.$(".hljs-button,.signin").remove();
      }
      if (window.$(".passport-login-container").length) {
        window.$(".passport-login-container").remove();
      }
    }
    requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
}();
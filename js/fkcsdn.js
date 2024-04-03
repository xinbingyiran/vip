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
  setTimeout(()=>{
      if(window && window.$ && window.$("#content_views"))
      {
          window.$("#content_views").unbind("copy").unbind("keydown");
          if(window.csdn && window.csdn.copyright){ window.csdn.copyright.textData = ''; }
          window.$(".look-more-preCode,.contentImg-no-view").click()
          window.$("div.blog-content-box").unbind("copy");
          window.$(".hljs-button,.signin").remove()
          window.$("pre").attr("contentEditable",true);
      }
  },1000);
}();
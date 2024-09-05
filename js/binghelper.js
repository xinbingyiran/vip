// ==UserScript==
// @name         bing纯净版
// @namespace    http://tampermonkey.net/
// @version      2024-05-31
// @description  bing纯净版
// @author       You
// @match        https://*.bing.com/search?*
// @icon         https://cn.bing.com/sa/simg/favicon-trans-bg-blue-mg-png.png
// @grant        window.onurlchange
// ==/UserScript==

!function() {
    'use strict';
    const check = ()=>{
        const newhref = window.location.href.split('?').map(m=>m.split('&').filter(f=>f.match(/^https?:\/\/|^q=|^first=/ig)).join('&')).join('?');
        if(newhref !== window.location.href){
            window.location.replace(newhref);
        }
    };
    if (window.onurlchange === null) {
        window.addEventListener('urlchange', check);
    }
    check();
}();
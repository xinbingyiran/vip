// ==UserScript==
// @name         解决百度云大文件下载限制
// @namespace    undefined
// @version      0.0.6
// @description  一行代码，解决百度云大文件下载限制
// @author       funianwuxin
// @match        http://pan.baidu.com/*
// @match        https://pan.baidu.com/*
// @match        http://yun.baidu.com/*
// @match        https://yun.baidu.com/*
// @match        https://eyun.baidu.com/*
// @run-at       document-start
// @grant        none
// ==/UserScript==
/* jshint -W097 */
/* https://greasyfork.org/zh-CN/scripts/17800-%E8%A7%A3%E5%86%B3%E7%99%BE%E5%BA%A6%E4%BA%91%E5%A4%A7%E6%96%87%E4%BB%B6%E4%B8%8B%E8%BD%BD%E9%99%90%E5%88%B6 */
'use strict';

Object.defineProperty(Object.getPrototypeOf(navigator),'platform',{get:function(){return 'sb_baidu';}})


(function(){
var href=location.href;
/http:/.test(href)?location.href='https'+href.slice(4):0;
}());

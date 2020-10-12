(function(){
	var vipUrls = [
		{name:"SVIP",url:"http://vip.ggxx.ml/svip/index.html?"}
	];
	//设置cookie  
	function setCookie(cname, cvalue, exdays) {  
		var d = new Date();  
		d.setTime(d.getTime() + (exdays*24*60*60*1000));  
		var expires = "expires="+d.toUTCString();  
		document.cookie = cname + "=" + cvalue + "; " + expires;  
	}  
	//获取cookie  
	function getCookie(cname) {
		var name = cname + "=";  
		var ca = document.cookie.split(';');  
		for(var i=0; i<ca.length; i++) {  
			var c = ca[i];  
			while (c.charAt(0)==' ') c = c.substring(1);  
			if (c.indexOf(name) != -1) return c.substring(name.length, c.length);  
		}  
		return "";  
	} 
	function vipLink(urlLink,url,id) {
		if(!urlLink){return;}
		if(!url){return;}
		var turl = urlLink +  encodeURIComponent(url);
		if(id && id >= 0){
			chrome.tabs.update(id,{url:turl}); 
		}
		else{
			chrome.tabs.create({url:turl});
		}
	}	

	function initMenu()
	{
		for (var i in vipUrls)
		{
			var vipUrl = vipUrls[i];
			chrome.contextMenus.create({
				"title": vipUrl.name,
				"contexts":["link"],
				"onclick": (function(urlLink){
					return function (info,tab){
						setCookie("vipUrl",urlLink,30);
						if(info){
							vipLink(urlLink,info.linkUrl,-1);
						}
					};
				})(vipUrl.url)
			}); 
		}
		chrome.browserAction.onClicked.addListener(function(tab) {
			if(tab){
				var urlLink = getCookie("vipUrl") || vipUrls[0].url;
				vipLink(urlLink,tab.url,tab.id);
			}
		});
	}

	function findfilename(url)
	{
		var furl = new URL(url);
		var pname = furl.pathname;
		if(pname == "/")
		{
			return "download";
		}
		var name = pname.slice(pname.lastIndexOf("/") + 1) || pname.slice(pname.lastIndexOf("/",pname.length-2) + 1,-1);
		return name?decodeURIComponent(name):"download";
	}
	
	initMenu();

	//接收消息
	var types =["audio","video","application"];
	
	chrome.webRequest.onHeadersReceived.addListener(function(details){
		var id = details.tabId;
		if(id >= 0)		{
			var ct = details.responseHeaders.find(function(e){return e.name.toLowerCase() == "content-type";})
			if(!ct){return;}
			var v = ct.value.toLowerCase().split(/;/)[0];
			var vt = v.split(/\//);
			if(vt[0] != "audio" && vt[0] != "video" && vt[0] != "application"){return;}		
			if(vt[0] == "application"){
				if(vt[1].indexOf("text") >= 0 || vt[1].indexOf("script") >= 0){
					return;
				}
			}
			var fn = details.responseHeaders.find(function(e){return e.name.toLowerCase() == "content-disposition";})
			var filename = "";
			if(fn){
				var m = fn.value.toLowerCase().match(/filename=(.*)/);
				if(m){
					filename = m[1].trim();
				}
			}
			if(!filename){
				filename = findfilename(details.url);
			}
			
			if(v){
				chrome.tabs.sendMessage(details.tabId,{"type":v,"name":filename,"url":details.url}); 
			}
		}
	}, {urls: ["<all_urls>"]}, ["responseHeaders"]);
})();
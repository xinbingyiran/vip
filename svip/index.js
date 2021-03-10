(function () {
	//设置cookie  
	function setCookie(cname, cvalue, exdays) {
		var d = new Date();
		d.setTime(d.getTime() + (exdays * 24 * 60 * 60 * 1000));
		var expires = "expires=" + d.toUTCString();
		document.cookie = cname + "=" + cvalue + "; " + expires;
	}
	//获取cookie  
	function getCookie(cname) {
		var name = cname + "=";
		var ca = document.cookie.split(';');
		for (var i = 0; i < ca.length; i++) {
			var c = ca[i];
			while (c.charAt(0) == ' ') c = c.substring(1);
			if (c.indexOf(name) != -1) return c.substring(name.length, c.length);
		}
		return "";
	}
	
	function loadFrame(sourceurl) {
		var view = document.getElementById("viewframe");
		view.src = sourceurl;
	}

	var floatdiv, contentdiv;

	function viewfloatdiv() {
		floatdiv.style.opacity = 0.5;
		contentdiv.style.display = "block";
	}

	function unviewfloatdiv() {
		floatdiv.style.opacity = 0.1;
		contentdiv.style.display = "none";
		stopmove();
	}

	function domove(e) {
		var mx = e.clientX - posX;
		var my = e.clientY - posY;
		posX = e.clientX;
		posY = e.clientY;
		floatdiv.style.left = +floatdiv.style.left.slice(0, -2) + mx + "px";
		floatdiv.style.top = +floatdiv.style.top.slice(0, -2) + my + "px";
	}

	function startmove(e) {
		posX = e.clientX;
		posY = e.clientY;
		floatdiv.onmousemove = domove;
	}

	function stopmove() {
		floatdiv.onmousemove = null;
	}

	function goVip() {
		var url = document.querySelector('#url').value;
		window.top.location.href = "DPlayer.html?" + url;
		return false;
	};

	function checkEnter(e) {
		if (e.keyCode == 13) {
			goVip();
		}
	}
	function InitDiv() {
		document.querySelector('#url').onkeydown = checkEnter;
		document.querySelector('#gourl').onclick = goVip;
		floatdiv = document.getElementById("_idxfloatdiv");
		contentdiv = document.getElementById("_idxcontentdiv");
		floatdiv.onmousedown = startmove;
		floatdiv.onmouseup = stopmove;
		floatdiv.onmouseenter = viewfloatdiv;
		floatdiv.onmouseleave = unviewfloatdiv;		
		for (var i in spUrls) {
			var spUrl = spUrls[i][1];
			var httpTarget = "_blank";
			var item = document.createElement("a");
			item.setAttribute("href", spUrl);
			item.setAttribute("target", httpTarget);
			item.innerHTML = spUrls[i][0];
			floatdiv.appendChild(item);
			var textNode = document.createTextNode("	");
			floatdiv.appendChild(textNode);
		}
		unviewfloatdiv();		
		var turl = window.location.hash.slice(1);
		if (turl.slice(0, 4) != "http") {
			turl = window.location.search.slice(1);
		}
		if (turl.slice(0, 4) != "http") {
			turl = "";
		}
		var url = decodeURIComponent(turl);
		while (url && url != turl) {
			turl = url;
			url = decodeURIComponent(turl);
		}
		if (url && (url.slice(0, 7) != "http://") && (url.slice(0, 8) != "https://")) {
			url = "";
		}
		if (!url) {
			loadFrame("time.html");
			return;
		}
		var durl = url;
		var initUrl = getCookie("url");
		var containsUrl = false;
		var firstItem,firstUrl;
		for (var i in vipUrls) {
			var vipUrl = vipUrls[i][1];
			var httpTarget = "viewframe";
			var useBlank = window.location.protocol == "https:" && vipUrl.startsWith("http:");
			var item = document.createElement("a");
			if(!firstItem){
				firstItem = item;
				firstUrl = vipUrl;
			}
			if (useBlank) {
				httpTarget = "_blank";
			}
			else {
				if (initUrl == vipUrl) {
					containsUrl = true;
					item.style.background="green";
				}
			}
			item.onclick = (function (obj) {
				return function () {
					if (obj.useBlank) {
						loadFrame("time.html");
					}
					else {
						setCookie("url", obj.url, 1);
						this.parentNode.childNodes.forEach(function(node){
							if(node.style)node.style.background="";
						});
						this.style.background="green";
					}
				};
			})({ url: vipUrl, useBlank: useBlank });
			item.setAttribute("href", vipUrl + durl);
			item.setAttribute("target", httpTarget);
			item.innerHTML = vipUrls[i][0];
			contentdiv.appendChild(item);
			var textNode = document.createTextNode("	");
			contentdiv.appendChild(textNode);
		}
		if (!initUrl || !containsUrl) {
			initUrl = firstUrl;
			firstItem.style.background="green";
		}
		loadFrame(initUrl + durl);
	}
	window.onload = InitDiv;	
	var vipUrls = [
		["1907", "https://z1.m1907.cn/?jx="],
		["音萌", "https://api.v6.chat/?url="],
		["BL解析", "https://vip.bljiex.com/?v="],
		["思古", "https://api.sigujx.com/?url="],
		["ivito", "https://jx.ivito.cn/?url="],
		["lhh", "https://api.lhh.la/vip/?url="],
		["41", "https://jx.f41.cc/?url="],
		["ckmov", "https://www.ckmov.vip/api.php?url="],
		["mw0", "https://jx.mw0.cc/?url="],
		["okjs", "https://okjx.cc/?url="],
		["金桥解析", "http://jqaaa.com/jx.php?url="],
		["DuPlay", "http://jx.du2.cc/?url="],
		["618G", "http://jx.618g.com/?url="],
		["百域阁", "http://api.baiyug.vip/?url="],
		["1717云", "http://www.1717yun.com/jx/vip?url="],
		["花园解析", "http://j.zz22x.com/jx/?url="]
	];
	var spUrls = [
		["M1907", "https://z1.m1907.cn"],
		["线报坊", "http://tv.iqxbf.com"],
        ["草民电影网", "https://www.cmdyhd.com"],
        ["三米影视", "https://www.smmy365.com"],
        ["难看影院", "https://www.nksee.com"],
        ["电影盒子", "https://www.dyhz8.com"],
        ["云播TV", "https://www.yunbtv.com"],
        ["高清云影视", "https://www.gqytv.com"],
        ["神马影院", "https://www.3s8m.com"],
        ["淘影网", "http://www.tyw8188.com"],
        ["1090", "http://1090ys.com"],
        ["88电影网", "http://www.28ddy.com/"]
	];
})();
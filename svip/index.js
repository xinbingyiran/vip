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
	var vipUrls = {
		"音萌": "https://api.v6.chat/?url=",
		"heimijx": "https://www.heimijx.com/jx/api/?url=",
		"思古": "https://api.bbbbbb.me/jx/?url=",
		"思古2": "https://api.sigujx.com/?url=",
		"流氓凡": "https://jx.wslmf.com/?url=",
		"47Player": "https://api.47ks.com/webcloud/?v=",
		"金桥解析": "http://jqaaa.com/jx.php?url=",
		"石头解析": "https://jiexi.071811.cc/jx.php?url=",
		"598110": "http://jx.598110.com/?url=",
		"jlsprh": "http://vip.jlsprh.com/?url=",
		"618阁": "http://jx.618ge.com/?url=",
		"drgxj": "http://jx.drgxj.com/?url=",
		"DuPlay": "http://jx.du2.cc/?url=",
		"人人解析": "https://vip.mpos.ren/v/?url=",
		"618G": "http://jx.618g.com/?url=",
		"大亨影院解析": "http://jx.cesms.cn/?url=",
		"乐乐云": "https://660e.com/?url=",
		"无名小站": "https://www.administratorw.com/admin.php?url=",
		"ODFLV": "https://yun.odflv.com/?url=",
		"百域阁": "http://api.baiyug.vip/index.php?url=",
		"够低调": "http://goudidiao.com/?url=",
		"猫云": "https://jx.maoyun.tv/index.php?id=",
		"1717云": "http://www.1717yun.com/jx/vip/index.php?url=",
		"旋风解析": "http://api.xfsub.com/index.php?url=",
		"VIP看看": "http://q.z.vip.totv.72du.com/?url=",
		"163人": "http://jx.api.163ren.com/vod.php?url=",
		"CKFLV": "http://www.0335haibo.com/tong.php?url=",
		"无名小站2": "http://www.wmxz.wang/video.php?url=",
		"眼睛会下雨": "http://www.vipjiexi.com/yun.php?url=",
		"人人发布": "https://hhh.qqplayer.cn/beac.php?url=",
		"花园解析": "http://j.zz22x.com/jx/?url=",
		"那片解析": "http://api.nepian.com/ckparse/?url=",
		"BL解析": "https://vip.bljiex.com/?v="
	};
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

	function domove(ev) {
		var mx = event.clientX - posX;
		var my = event.clientY - posY;
		posX = event.clientX;
		posY = event.clientY;
		floatdiv.style.left = +floatdiv.style.left.slice(0, -2) + mx + "px";
		floatdiv.style.top = +floatdiv.style.top.slice(0, -2) + my + "px";
	}

	function startmove(e) {
		posX = event.clientX;
		posY = event.clientY;
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

	function checkEnter() {
		if (event.keyCode == 13) {
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
		unviewfloatdiv();
		//var durl = encodeURIComponent(url);
		if (!url) {
			loadFrame("time.html");
			return;
		}
		var durl = url;
		var initUrl = getCookie("url");
		var containsUrl = false;
		var firstItem,firstUrl;
		for (var urlKey in vipUrls) {
			var vipUrl = vipUrls[urlKey];
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
			//item.setAttribute("href","javascript:void(0);");
			item.setAttribute("href", vipUrl + durl);
			item.setAttribute("target", httpTarget);
			item.innerHTML = urlKey;
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
})();
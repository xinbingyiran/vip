(function (){
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
	var vipUrls = [
		{name:"金桥解析",url:"https://jqaaa.com/jx.php?url="},
		{name:"花园影视",url:"https://j.zz22x.com/jx/?url="},
		{name:"47Player",url:"https://api.47ks.com/webcloud/?v="},
		{name:"强强解析",url:"https://000o.cc/jx/ty.php?url="},
		{name:"206云解析",url:"https://206dy.com/vip.php?url="},
		{name:"ODFLV",url:"http://aikan-tv.com/?url="},
		{name:"无名小站",url:"http://www.wmxz.wang/video.php?url="},
		{name:"思古",url:"http://api.bbbbbb.me/jx/?url="},
		//{name:"思古2",url:"http://api.bbbbbb.me/svip/v.php?url="},
		//{name:"思古3",url:"http://api.bbbbbb.me/jiexi/?url="},
		//{name:"思古4",url:"http://api.bbbbbb.me/playm3u8/?url="},
		//{name:"思古5",url:"http://api.bbbbbb.me/yunjx/?url="},
		//{name:"思古6",url:"http://api.bbbbbb.me/vip/?url="},
		//{name:"思古7",url:"http://api.bbbbbb.me/yun/?url="},
		//{name:"思古8",url:"http://api.bbbbbb.me/ipsign/player.php?v="},
		//{name:"思古9",url:"http://api.bbbbbb.me/jx/2mm.php?url="},
		//{name:"速度牛",url:"http://api.wlzhan.com/sudu/?url="},
	];
	var turl = window.location.hash.slice(1);
	if(turl.slice(0,4) != "http")
	{
		turl = window.location.search.slice(1);
	}
	if(turl.slice(0,4) != "http")
	{
		turl = "";
	}
	var url = decodeURIComponent(turl);
	while(url && url != turl)
	{
		turl = url;
		url = decodeURIComponent(turl);
	}		
	if(url && (url.slice(0,7) != "http://") && (url.slice(0,8) != "https://"))
	{
		url = "";
	}
	function loadFrame(sourceurl)
	{
		var view = document.getElementById("viewframe");
		view.src = sourceurl;
	}

	var floatdiv,contentdiv;

	function viewfloatdiv()
	{
		floatdiv.style.opacity = 0.5;
		contentdiv.style.display = "block";
	}

	function unviewfloatdiv()
	{
		floatdiv.style.opacity = 0.1;
		contentdiv.style.display = "none";
		stopmove();
	}

	function domove(ev)
	{
		var mx = event.clientX - posX;
		var my = event.clientY - posY;
		posX = event.clientX;
		posY = event.clientY;
		floatdiv.style.left = +floatdiv.style.left.slice(0,-2) + mx +  "px";
		floatdiv.style.top = +floatdiv.style.top.slice(0,-2) + my +  "px";
	}

	function startmove(e)
	{
		posX = event.clientX;
		posY = event.clientY;
		floatdiv.onmousemove = domove;
	}

	function stopmove()
	{
		floatdiv.onmousemove = null;
	}

	function goVip() {
		var url = document.querySelector('#url').value;
		window.top.location.href = "DPlayer.html?" + url;
		return false;
	};

	function checkEnter(){
		if(event.keyCode==13){
			goVip();
		}
	}
	function InitDiv()
	{		
		document.querySelector('#url').onkeydown=checkEnter;
		document.querySelector('#gourl').onclick=goVip;
		floatdiv = document.getElementById("_idxfloatdiv");
		contentdiv = document.getElementById("_idxcontentdiv");
		floatdiv.onmousedown= startmove;
		floatdiv.onmouseup = stopmove;
		floatdiv.onmouseenter = viewfloatdiv;
		floatdiv.onmouseleave = unviewfloatdiv;
		unviewfloatdiv();
		//var durl = encodeURIComponent(url);
		if(!url)
		{
			loadFrame("time.html");
			return;
		}
		var durl = url;
		var initUrl = getCookie("url");
		var containsUrl = false;
		for (var i in vipUrls)
		{
			var vipUrl = vipUrls[i];
			var httpTarget = "viewframe";
			var useBlank = window.location.protocol == "https:" && vipUrl.url.startsWith("http:");
			if(useBlank)
			{
				httpTarget = "_blank";
			}
			else
			{
				if(initUrl == vipUrl.url)
				{
					containsUrl = true;
				}
			}
			var item = document.createElement("a");
			item.onclick = (function(obj){
				return function(){
					if(obj.useBlank)
					{
						loadFrame("time.html");	
					}
					else
					{
						setCookie("url",obj.url,1);
					}
				};
			})({url:vipUrl.url,useBlank:useBlank});
			//item.setAttribute("href","javascript:void(0);");
			item.setAttribute("href",vipUrl.url + durl);
			item.setAttribute("target",httpTarget);
			item.innerHTML = vipUrl.name;
			contentdiv.appendChild(item);
			var textNode = document.createTextNode("	");
			contentdiv.appendChild(textNode);				
		}
		if(!initUrl || !containsUrl)
		{
			initUrl = vipUrls[0].url;
		}
		loadFrame(initUrl + durl);
	}
	window.onload = InitDiv;
})();
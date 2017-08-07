(function(){

	var posX,posY,list,floatdiv,contentdiv,clearbt;
	list = [];
	
	floatdiv = document.createElement("div");
	contentdiv = document.createElement("div");
	clearbt = document.createElement("span");

    function clearContentdiv()
    {
	    list = [];
	    contentdiv.innerHTML = "";
		floatdiv.style.display = "none";
		unviewfloatdiv();
    }

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
		floatdiv.style.right = +floatdiv.style.right.slice(0,-2) - mx +  "px";
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

	function addLog(vt,url) {
		if(list.includes(url)){return;}
		var fname = findfilename(url);
		list.push(url);
		var l = document.createElement("span");
		l.innerHTML = vt + ": ";
		contentdiv.appendChild(l);
		var a = document.createElement("a");
		a.target = "_blank";
		a.style = "color:red;margin-right:20px;";
		a.href = url;
		a.text = fname;
		contentdiv.appendChild(a);
		var br = document.createElement("br");
		contentdiv.appendChild(br);
		floatdiv.style.display = "block";
	}
	chrome.extension.onMessage.addListener(function(msg, sender, callback) {
		addLog(msg.type, msg.url);
	});

	document.addEventListener('DOMContentLoaded', function(){
		clearbt.onmousedown = clearContentdiv;
		floatdiv.onmouseenter = viewfloatdiv;
		floatdiv.onmouseleave = unviewfloatdiv;
		floatdiv.onmousedown= startmove;
		floatdiv.onmouseup = stopmove;
		floatdiv.style.display = "none";
		unviewfloatdiv();
		document.body.appendChild(floatdiv);
	});
	floatdiv.id = "_cttfloatdiv";
	floatdiv.style = "color: Red; font-weight: bolder;border: solid 1px #CCCCCC;position: fixed; z-index: 2147483647;top:10px;right:10px;display:block;background-color: black";
	contentdiv.id = "_cttcontentdiv";
	floatdiv.innerHTML = "列表：";
	contentdiv.style = "overflow-y: scroll; overflow-x:hidden;width:480px;height:360px;border: solid 1px #CCCCCC;";
	clearbt.innerHTML = "清除";
	floatdiv.appendChild(clearbt);
	floatdiv.appendChild(contentdiv);
})();
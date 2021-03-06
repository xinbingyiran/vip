﻿(function(){

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

	function domove(e)
	{
		var mx = e.clientX - posX;
		var my = e.clientY - posY;
		posX = e.clientX;
		posY = e.clientY;
		floatdiv.style.right = +floatdiv.style.right.slice(0,-2) - mx +  "px";
		floatdiv.style.top = +floatdiv.style.top.slice(0,-2) + my +  "px";
	}

	function startmove(e)
	{
		posX = e.clientX;
		posY = e.clientY;
		floatdiv.onmousemove = domove;
	}

	function stopmove()
	{
		floatdiv.onmousemove = null;
	}

	function addLog(msg) {
		var type = msg.type;
		var name = msg.name;
		var url = msg.url;
		if(list.includes(url)){return;}
		list.push(url);
		var l = document.createElement("span");
		l.innerHTML = type + ": ";
		contentdiv.appendChild(l);
		var a = document.createElement("a");
		a.target = "_blank";
		a.style = "color:red;margin-right:20px;";
		a.href = url;
		a.text = name;
		contentdiv.appendChild(a);
		var br = document.createElement("br");
		contentdiv.appendChild(br);
		floatdiv.style.display = "block";
	}
	chrome.extension.onMessage.addListener(function(msg, sender, callback) {
		addLog(msg);
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
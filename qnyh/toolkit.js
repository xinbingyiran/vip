var xlinfo = null;
var xltype = null;
var url = "https://jsonbox.io/qian_nv_you_hun__da_ti_qi";


var updatet = function () {
	var items = {
		type: xltype,
		question: new $("#tkey").val(),
		opt1: $("#tvalue").val()
	};
	updatet2(items);
}
var isArray = function (i) {
	return i && typeof i === 'object' && Array == i.constructor;
}
var updatelocal = function (type, items, perpack, timeout) {
	var array = [];
	items.forEach(item => array.push({
		type: type,
		question: item.question,
		opt1: item.opt1
	}));
	perpack = perpack || 100;
	timeout = timeout || 1000;
	for (var i = 0; i < array.length; i += perpack) {
		delayupdatelocal(array.slice(i, i + perpack), i * timeout / perpack);
	}
}
var delayupdatelocal = function (items, timeout) {
	setTimeout(() => {
		updatet2(items)
	}, timeout);
}
var updatet2 = function (items, direct) {
	if (!items) {
		$("#tresult").text("empty items");
	}
	if (direct || isArray(items)) {
		$.ajax({
			url: url,
			type: "POST",
			headers: {
				"Accept": "application/json",
				"Content-Type": "application/json",
			},
			data: JSON.stringify(items),
			error: function (jqXHR, textStatus, errorThrown) {
				$("#tresult").text(jqXHR.responseText || textStatus);
			},
			success: function (data, textStatus, jqXHR) {
				if (data.message) {
					$("#tresult").text(data.message);
				}
				else {
					$("#tresult").text((isArray(items) ? items.length : items.question) + " 添加成功！");
				}
			}
		});
		return;
	}
	$.ajax({
		url: url + "?q=type:" + items.type + ",question:" + encodeURIComponent(items.question),
		type: "GET",
		headers: {
			"Accept": "application/json",
			"Content-Type": "application/json",
		},
		error: function (jqXHR, textStatus, errorThrown) {
			$("#tresult").text(jqXHR.responseText || textStatus);
		},
		success: function (data, textStatus, jqXHR) {
			if (data.length) {
				$("#tresult").text("已有数据");
			}
			else {
				updatet2(items, true)
			}
		}
	});
}

var pullt = function (next, limit) {
	limit = limit || 100;
	var pullurl = url + "?limit=" + limit;
	if (next) {
		pullurl += "&skip=" + next;
	}
	$.ajax({
		url: pullurl,
		type: "GET",
		headers: {
			"Accept": "application/json",
			"Content-Type": "application/json",
		},
		error: function (jqXHR, textStatus, errorThrown) {
			$("#tresult").text(jqXHR.responseText || textStatus);
		},
		success: function (data, textStatus, jqXHR) {
			if (data.length) {
				mergeData(data);
				if (data.length == limit) {
					pullt((next || 0) + limit);
					return;
				}
			}
			$("#tresult").text("完成新记录获取:" + ((next || 0) + data.length));
		}
	});
}

var mergeData = function (data) {
	data.forEach(item => {
		var result = 0;
		var type = item["type"];
		var key = item["question"];
		var value = item["opt1"];
		switch (type) {
			case "xjl":
				result = mergeToInfo(xjl.xlinfo, key, value)
				break;
			case "kj":
				result = mergeToInfo(kj.xlinfo, key, value)
				break;
			case "bld":
				result = mergeToInfo(bld.xlinfo, key, value)
				break;
		}
		if (result) {
			console.log(type + " + " + key + " : " + value);
		}
	});
}

var mergeToInfo = function (infos, key, value) {
	for (var i in infos) {
		if (infos[i].question == key)
			return 0;
	}
	infos.push({ "question": key, "opt1": value });
	return 1;
}

var reInit = function () {
	clean();
}

var loadxjl = function () {
	xlinfo = xjl.xlinfo;
	xltype = "xjl";
	reInit();
}
var loadkj = function () {
	xlinfo = kj.xlinfo;
	xltype = "kj";
	reInit();
}
var loadbld = function () {
	xlinfo = bld.xlinfo;
	xltype = "bld";
	reInit();
}

$(document.body).ready(function () {
	loadxjl();
	$(".syb>a").click(function () {
		$(this).addClass('cur').siblings().removeClass('cur');
		switch ($(this).index()) {
			case 0:
				loadxjl();
				break;
			case 1:
				loadkj();
				break;
			case 2:
				loadbld();
				break;
			default:
				break;
		}
	});
	new ClipboardJS('.btn', {
		text: function (trigger) {
			return trigger.innerText;
		}
	});
});

//载入数据
var loadData = function (str) {
	var items = filterData(str);
	var result = '';
	if (items.length) {
		if (items.length != 0) {
			for (var index = 0; index < items.length; index++) {
				if (index == 12)
					break;
				var item = items[index];
				result += '<tr><td width="66%">' + item['question'] + '</td><td align="center" class="btn">' + item['opt1'] + '</td></tr>';
			}
		}
		$("#result").html(result);
		$("#result").show();
	} else {
		$("#result").html('<tr><td colspan="2">没有搜索到相关的结果!</td></tr>');
		$("#result").show();
	}
}

var lastValue = "";
var searchData = function (obj, e) {
	if (obj.value == lastValue) {
		return;
	}
	lastValue = obj.value;
	loadData(obj.value);
}

//填充数据
var filterData = function (str) {
	if (typeof (str) == "undefined" || str.length == 0) {
		return xlinfo;
	}
	var result = [];
	for (var i in xlinfo) {
		if (xlinfo[i].question.indexOf(str) < 0)
			continue;
		result.push(xlinfo[i]);
	}
	return result;
}

var clean = function () {
	document.getElementById("keywords").value = "";
	loadData("");
}

var fd = function (arr) {
	var mid = [];
	arr.forEach(i => {
		var m = i.question;
		if (m && !mid[m]) {
			mid[m] = i;
		}
	});
	var result = [];
	var keys = Object.keys(mid).sort();
	var text = "";
	for (const key in keys) {
		text += JSON.stringify(mid[keys[key]]) + ",<br/>";
		result.push(mid[keys[key]]);
	}
	document.body.innerHTML = text;
	return;
}
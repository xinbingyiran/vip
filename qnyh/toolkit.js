var xlinfo = null;
var xltype = null;
var APIKey = "3d_vDQTCImnVorV7N0Z--Q";
var APISecret = "-t_kEH1yJNJYuBRhIEFeJA";
var auth = btoa(APIKey + ":" + APISecret);
var bdid = "mwP8fN";
var url = "https://jinshuju.net/f/" + bdid;
var apiurl = "https://jinshuju.net/api/v1/forms/" + bdid;

var updatet = function () {
	$.ajax({
		url: apiurl,
		type: "POST",
		headers: {
			"Accept": "application/json",
			"Content-Type": "application/json",
		},
		beforeSend: function (xhr) {
			xhr.setRequestHeader ("Authorization", "Basic " + auth);
		},
		data: {
			"类型": xltype,
			"问题": $("#tkey").val(),
			"答案": $("#tvalue").val(),
		  },
		error: function (jqXHR, textStatus, errorThrown) {
			$("#tresult").text(textStatus);
		},
		success: function (data, textStatus, jqXHR) {
			$("#tresult").text(JSON.stringify(data));
		}
	});
}


$(document.body).ready(function () {
	$(".syb>a").click(function () {
		$(this).addClass('cur').siblings().removeClass('cur');
		switch ($(this).index()) {
			case 0:
				xlinfo = xjl.xlinfo;
				xltype = "行酒令";
				clean();
				break;
			case 1:
				xlinfo = kj.xlinfo;
				xltype = "科举";
				clean();
				break;
			case 2:
				xlinfo = bld.xlinfo;
				xltype = "保灵丹";
				clean();
				break;
			default:
				break;
		}
	});
	xlinfo = xjl.xlinfo;
	xltype = "行酒令";
	loadData($("#keywords").val());
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
				result += '<tr><td width="66%">' + item['question'] + '</td><td align="center">' + item['opt1'] + '</td></tr>';
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
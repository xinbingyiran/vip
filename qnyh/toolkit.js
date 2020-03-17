var xlinfo = null;
var xltype = null;
var APIKey = "3d_vDQTCImnVorV7N0Z--Q";
var APISecret = "-t_kEH1yJNJYuBRhIEFeJA";
var auth = btoa(APIKey + ":" + APISecret);
var bdid = "mwP8fN";
var url = "https://jinshuju.net/f/" + bdid;
var apiurl = "https://jinshuju.net/api/v1/forms/" + bdid;
var geturl = "https://jinshuju.net/api/v1/forms/" + bdid + "/entries";

var fields = { "类型": "field_3", "问题": "field_1", "答案": "field_2" };
var choices = { "行酒令": "yd7l", "科举": "xTH0", "保灵丹": "De26" };

var updatet = function () {
	var datas = {};
	datas[fields["类型"]] = choices[xltype];
	datas[fields["问题"]] = $("#tkey").val();
	datas[fields["答案"]] = $("#tvalue").val();
	$.ajax({
		url: apiurl,
		type: "POST",
		headers: {
			"Accept": "application/json",
			"Content-Type": "application/json",
		},
		beforeSend: function (xhr) {
			xhr.setRequestHeader("Authorization", "Basic " + auth);
		},
		data: JSON.stringify(datas),
		error: function (jqXHR, textStatus, errorThrown) {
			$("#tresult").text($.parseJSON(jqXHR.responseText).error_description);
		},
		success: function (data, textStatus, jqXHR) {
			if (data.error_description) {
				$("#tresult").text(data.error_description);
			}
			else {
				$("#tresult").text("成功！");
			}
		}
	});
}

var pullt = function (next) {
	var url = next ? (geturl + "?next=" + next) : geturl;
	$.ajax({
		url: url,
		type: "GET",
		headers: {
			"Accept": "application/json",
			"Content-Type": "application/json",
		},
		beforeSend: function (xhr) {
			xhr.setRequestHeader("Authorization", "Basic " + auth);
		},
		error: function (jqXHR, textStatus, errorThrown) {
			$("#tresult").text($.parseJSON(jqXHR.responseText).error_description);
		},
		success: function (data, textStatus, jqXHR) {
			mergeData(data.data);
			if (data.next) {
				pullt(data.next);
			}
		}
	});
}

var mergeData = function (data) {
	data.forEach(item => {
		var result = 0;
		var type = item[fields["类型"]];
		var key = item[fields["问题"]];
		var value = item[fields["答案"]];
		switch (type) {
			case "行酒令":
				result = mergeToInfo(xjl.xlinfo, key, value)
				break;
			case "科举":
				result = mergeToInfo(kj.xlinfo, key, value)
				break;
			case "保灵丹":
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
	xltype = "行酒令";
	reInit();
}
var loadkj = function () {
	xlinfo = kj.xlinfo;
	xltype = "科举";
	reInit();
}
var loadbld = function () {
	xlinfo = bld.xlinfo;
	xltype = "保灵丹";
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
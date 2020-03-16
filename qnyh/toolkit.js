var xlinfo = null;
$(document.body).ready(function () {
    $(".syb>a").click(function () {
        $(this).addClass('cur').siblings().removeClass('cur');
        switch ($(this).index()) {
            case 0: xlinfo = xjl.xlinfo; clean(); break;
            case 1: xlinfo = kj.xlinfo; clean(); break;
            case 2: xlinfo = bld.xlinfo; clean(); break;
            default: break;
        }
    });
	xlinfo = xjl.xlinfo;
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

var fd = function (arr)
{
	var mid = [];
	arr.forEach(i => {
		var m = i.question;
		if(m && !mid[m])
		{
			mid[m] = i;
		}
	});
	var result=[];
	var keys = Object.keys(mid).sort();
	var text = "";
	for (const key in keys) {
		text += JSON.stringify(mid[keys[key]]) + ",<br/>";
		result.push(mid[keys[key]]);
	}
	document.body.innerHTML = text;
	return;
}
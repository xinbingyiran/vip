var week = ['星期天', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六'];
var timerID = setInterval(updateTime, 1000);
updateTime();
function updateTime() {
    var cd = new Date();
    document.querySelector('.time').innerHTML = zeroPadding(cd.getHours(), 2) + ':' + zeroPadding(cd.getMinutes(), 2) + ':' + zeroPadding(cd.getSeconds(), 2);
    document.querySelector('.date').innerHTML = zeroPadding(cd.getFullYear(), 4) + '-' + zeroPadding(cd.getMonth()+1, 2) + '-' + zeroPadding(cd.getDate(), 2) + ' ' + week[cd.getDay()];
};

function goVip() {
	var url = document.querySelector('#url').value;
	window.top.location.href = "index.html?" + url;
	return false;
};

function checkEnter(){
	if(event.keyCode==13){
		goVip();
	}
}

function zeroPadding(num, digit) {
    var zero = '';
    for(var i = 0; i < digit; i++) {
        zero += '0';
    }
    return (zero + num).slice(-digit);
}
document.querySelector('#url').onkeydown=checkEnter;
document.querySelector('#gourl').onclick=goVip;
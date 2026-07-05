!function () {
    if (globalThis._123_) {
        globalThis._123_.showDownload();
        return;
    }
    const _123 = globalThis._123_ = {};
    _123.crc32 = function (str) {
        var table = [];
        for (var i = 0; i < 256; i++) {
            var c = i;
            for (var k = 0; k < 8; k++) {
                c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1;
            }
            table[i] = c;
        }
        var utf8 = "";
        for (var i = 0; i < str.length; i++) {
            var code = str.charCodeAt(i);
            if (code < 128) {
                utf8 += String.fromCharCode(code);
            } else if (code < 2048) {
                utf8 += String.fromCharCode(code >> 6 | 192);
                utf8 += String.fromCharCode(code & 63 | 128);
            } else {
                utf8 += String.fromCharCode(code >> 12 | 224);
                utf8 += String.fromCharCode(code >> 6 & 63 | 128);
                utf8 += String.fromCharCode(code & 63 | 128);
            }
        }
        var crc = -1;
        for (var i = 0; i < utf8.length; i++) {
            crc = (crc >>> 8) ^ table[(crc ^ utf8.charCodeAt(i)) & 255];
        }
        return ((crc ^ -1) >>> 0).toString();
    };

    _123.dateToDigitString = function (timestamp) {
        var d = new Date(timestamp * 1000);
        return "" +
            d.getFullYear() +
            (d.getMonth() + 1 < 10 ? "0" + (d.getMonth() + 1) : d.getMonth() + 1) +
            (d.getDate() < 10 ? "0" + d.getDate() : d.getDate()) +
            (d.getHours() < 10 ? "0" + d.getHours() : d.getHours()) +
            (d.getMinutes() < 10 ? "0" + d.getMinutes() : d.getMinutes());
    };

    _123.digitsToMapped = function (digits) {
        var map = ["a", "d", "e", "f", "g", "h", "l", "m", "y", "i", "j", "n", "o", "p", "k", "q", "r", "s", "t", "u", "b", "c", "v", "w", "s", "z"];
        var result = "";
        for (var i = 0; i < digits.length; i++) {
            result += map[Number(digits[i])];
        }
        return result;
    };

    _123.nowTimestamp = function () {
        return Math.round(Date.now() / 1000);
    };

    _123.randomSeed = function () {
        return Math.round(1e7 * Math.random());
    };

    _123.sign = function (e, t, n) { // url,"web","3"
        var r = _123.nowTimestamp();
        var seed = _123.randomSeed();
        var sig1 = _123.crc32(_123.digitsToMapped(_123.dateToDigitString(r)));
        var sig2 = _123.crc32(r + "|" + seed + "|" + e + "|" + t + "|" + n + "|" + sig1);
        return [sig1, r + "-" + seed + "-" + sig2];
    };

    _123.verifySign = function (result, e, t, n, maxAgeSeconds) {
        maxAgeSeconds = maxAgeSeconds || 300;
        var sig1 = result[0];
        var parts = result[1].split("-");
        var r = Number(parts[0]);
        var seed = Number(parts[1]);
        var sig2 = parts[2];

        if (Math.abs(_123.nowTimestamp() - r) > maxAgeSeconds) {
            return { valid: false, reason: "expired" };
        }
        if (_123.crc32(_123.digitsToMapped(_123.dateToDigitString(r))) !== sig1) {
            return { valid: false, reason: "sig1 mismatch" };
        }
        if (_123.crc32(r + "|" + seed + "|" + e + "|" + t + "|" + n + "|" + sig1) !== sig2) {
            return { valid: false, reason: "sig2 mismatch" };
        }
        return { valid: true };
    };

    _123.getFileDownloadData = async function (s, a, t) {
        const content = JSON.stringify({ ShareKey: s, FileId: t.FileId, S3KeyFlag: t.S3KeyFlag, Size: t.Size, Etag: t.Etag }),
            path = "/b/api/v2/share/download/info",
            signData = _123.sign(path, "web", "3"),
            fullUrl = `${globalThis.location.origin}${path}?${signData[0]}=${signData[1]}`,
            opt = { method: "POST", body: content, headers: { Authorization: a, "Content-Type": "application/json;charset=UTF-8"} };

        return (await (await fetch(fullUrl, opt)).json()).data;
    };

    _123.getReact = function (selector) {
        return Object.entries(document.querySelector(selector)).find(([key]) => key.startsWith("__reactFiber$") || key.startsWith("__reactInternalInstance$"))[1];
    }

    _123.showDownload = async function () {
        const props = _123.getReact(".file-list-container").return.pendingProps;
        shareKey = props.info.ShareKey;
        fileList = props.isSingleFile ? props.serverFileList : _123.getReact(".file-list-container>div>div[class$=wrapper]").return.pendingProps.dataSource,
            authorToken = localStorage.getItem("authorToken").replace(/[\"\\]/g, ""),
            authorization = `Bearer ${authorToken}`;
        await Promise.all([fileList[0]].map(async item => {
            if (item.Etag) {
                var data = await _123.getFileDownloadData(shareKey, authorization, item);
                console.info(`文件：[${item.FileName ? item.FileName : item.FileId}] (${data.dispatchList[0].prefix}${data.downloadPath})`);
            }
        }));
    };

    _123.showDownload();
}();

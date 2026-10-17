!function () {
    if (globalThis._tianyi_) {
        globalThis._tianyi_.showDownload();
        return;
    }
    const tianyi = globalThis._tianyi_ = {};
    tianyi.md5 = (function () {
        function rotl(x, n) { return (x << n) | (x >>> (32 - n)); }
        function add32(a, b) { return (a + b) & 0xffffffff; }
        function F(x, y, z) { return (x & y) | (~x & z); }
        function G(x, y, z) { return (x & z) | (y & ~z); }
        function H(x, y, z) { return x ^ y ^ z; }
        function I(x, y, z) { return y ^ (x | ~z); }
        const S = [7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21];
        const K = [];
        for (let i = 0; i < 64; i++) K.push(Math.floor(Math.abs(Math.sin(i + 1)) * 0x100000000) & 0xffffffff);
        function strToBytes(s) {
            const b = [];
            for (let i = 0; i < s.length; i++) {
                let c = s.charCodeAt(i);
                if (c > 0xff) { b.push(c & 0xff); c >>= 8; }
                b.push(c);
            }
            return b;
        }
        function bytesToWords(b) {
            const w = new Array(b.length >>> 2);
            for (let i = 0; i < w.length; i++) w[i] = ((b[i * 4] | 0) | ((b[i * 4 + 1] | 0) << 8) | ((b[i * 4 + 2] | 0) << 16) | ((b[i * 4 + 3] | 0) << 24)) | 0;
            return w;
        }
        function wordsToHex(w) {
            let h = '';
            for (let i = 0; i < w.length; i++) {
                const v = w[i];
                for (let j = 0; j < 4; j++) h += ((v >>> (j * 8)) & 0xff).toString(16).padStart(2, '0');
            }
            return h;
        }
        return function md5(s) {
            const msg = strToBytes(unescape(encodeURIComponent(s)));
            const len = msg.length;
            const ml = len * 8;
            msg.push(0x80);
            while (msg.length % 64 !== 56) msg.push(0);
            msg.push(ml & 0xff, (ml >>> 8) & 0xff, (ml >>> 16) & 0xff, (ml >>> 24) & 0xff);
            msg.push(0, 0, 0, 0);
            let a0 = 0x67452301, b0 = 0xefcdab89, c0 = 0x98badcfe, d0 = 0x10325476;
            const w = bytesToWords(msg);
            for (let i = 0; i < w.length; i += 16) {
                let a = a0, b = b0, c = c0, d = d0;
                for (let j = 0; j < 64; j++) {
                    let f, g;
                    if (j < 16) { f = F(b, c, d); g = j; }
                    else if (j < 32) { f = G(b, c, d); g = (5 * j + 1) % 16; }
                    else if (j < 48) { f = H(b, c, d); g = (3 * j + 5) % 16; }
                    else { f = I(b, c, d); g = (7 * j) % 16; }
                    const tmp = add32(add32(add32(a, f), K[j]), w[i + g]);
                    a = d; d = c; c = b; b = add32(b, rotl(tmp, S[j]));
                }
                a0 = add32(a0, a); b0 = add32(b0, b); c0 = add32(c0, c); d0 = add32(d0, d);
            }
            return wordsToHex([a0, b0, c0, d0]);
        };
    })();

    tianyi.getSignature = e => tianyi.md5(Object.entries(e).map(([k, v]) => `${k}=${v}`).toSorted().join("&")).toString();

    tianyi.getFileDownloadUrl = async function (accessToken, item) {
        const { fileId, shareId } = item,
            timestamp = Date.now(),
            signature = tianyi.getSignature({ AccessToken: accessToken, Timestamp: timestamp, fileId: fileId, ...shareId && { dt: 1, shareId: shareId } }),
            url = "https://api.cloud.189.cn/open/file/getFileDownloadUrl.action?fileId=" + fileId + (shareId ? "&dt=1&shareId=" + shareId : ""),
            opt = { headers: { Accept: "application/json;charset=UTF-8", AccessToken: accessToken, Signature: signature, "Sign-Type": 1, Timestamp: timestamp } };

        return (await (await fetch(url, opt)).json()).fileDownloadUrl;
    };

    tianyi.getSelectedFileList = function () {
        const lvue = globalThis.document.querySelector(".c-file-list")?.__vue__,
            fvue = globalThis.document.querySelector(".info-detail")?.__vue__;
        return lvue ? lvue.selectLength ? lvue.selectedList : lvue.fileList : fvue ? Object.keys(fvue.fileDetail).length ? [fvue.fileDetail] : [] : [];
    };

    tianyi.showDownload = async function () {
        const fileList = tianyi.getSelectedFileList(),
            accessToken = (localStorage.getItem("accessToken") ?? (await (await fetch(`https://cloud.189.cn/api/open/oauth2/getAccessTokenBySsKey.action?noCache=${Math.random()}&sessionKey=${sessionStorage.getItem("sessionKey")}`, { headers: { "appkey": "600100422" } })).json()).accessToken).replace(/[\"\\]/g, "");
        await Promise.all(fileList.map(async item => !item.isFolder && console.info(`文件：[${item.fileName ? item.fileName : item.fileId}] (${item.downloadUrl ?? await tianyi.getFileDownloadUrl(accessToken, item)})`)));
    };

    tianyi.showDownload();
}();

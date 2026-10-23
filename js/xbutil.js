var xbutil = function () {
    var _keyStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
    var bytesToBase64 = input => {
        var output = [];
        var o = 0;
        var i = 0;
        var a1, a2, a3;
        var len = input.length;
        while (i < len) {
            a1 = input[i++] & 0xff;
            output[o++] = _keyStr[a1 >> 2];
            if (i < len) {
                a2 = input[i++] & 0xff;
                output[o++] = _keyStr[((a1 & 0x3) << 4) | (a2 >> 4)];
                if (i < len) {
                    a3 = input[i++] & 0xff;
                    output[o++] = _keyStr[(((a2 & 0xf)) << 2) | (a3 >> 6)];
                    output[o++] = _keyStr[a3 & 0x3f];
                } else {
                    output[o++] = _keyStr[((a2 & 0xf)) << 2];
                    output[o++] = '=';
                }
            } else {
                output[o++] = _keyStr[(a1 & 0x3) << 4];
                output[o++] = '=';
                output[o++] = '=';
            }
        }
        return output.join('');
    }

    var base64ToBytes = input => {
        var output = [];
        var o = 0;
        var i = 0;
        var a1, a2, a3, a4;
        input = input.replace(/[^A-Za-z0-9\+\/]/g, "");
        var len = input.length;
        while (i < len) {
            a1 = _keyStr.indexOf(input[i++]);
            if (i < len) {
                a2 = _keyStr.indexOf(input[i++]);
                output[o++] = (a1 << 2) | (a2 >> 4);
                if (i < len) {
                    a3 = _keyStr.indexOf(input[i++]);
                    output[o++] = ((a2 & 0xf) << 4) | ((a3 >> 2) & 0xf);
                    if (i < len) {
                        a4 = _keyStr.indexOf(input[i++]);
                        output[o++] = ((a3 & 3) << 6) | a4;
                    }
                }
                else {
                }
            }
        }
        output.length = o;
        return output;
    }

    var bytesToUtf = (array, type) => {
        var i = 0;
        var result = [];
        var r = 0;
        while (i < array.length) {
            if (type == "utf32le") {
                var cp = array[i] | (array[i + 1] << 8) | (array[i + 2] << 16) | (array[i + 3] << 24);
                i += 4;
                result[r++] = String.fromCodePoint(cp);
            } else if (type == "utf32be") {
                var cp = (array[i] << 24) | (array[i + 1] << 16) | (array[i + 2] << 8) | (array[i + 3]);
                i += 4;
                result[r++] = String.fromCodePoint(cp);
            } else if (type == "utf16le") {//D800-DFFF   DC00-DFFF
                var sh = array[i] | (array[i + 1] << 8);
                if (sh >= 0xd800 && sh <= 0xdbff) {
                    var sl = array[i + 2] | (array[i + 3] << 8);
                    if (sl >= 0xdc00 && sl <= 0xdfff) {
                        var cp = ((sl & 0x3ff) | ((sh & 0x3ff) << 10)) + 0x10000;
                        result[r++] = String.fromCodePoint(cp);
                        i += 4;
                    } else {
                        result[r++] = String.fromCodePoint(sh);
                        i += 2
                    }
                } else {
                    result[r++] = String.fromCodePoint(sh);
                    i += 2;
                }
            } else if (type == "utf16be") {
                var sh = (array[i] << 8) | array[i + 1];
                if (sh >= 0xd800 && sh <= 0xdbff) {
                    var sl = (array[i + 2] << 8) | array[i + 3];
                    if (sl >= 0xdc00 && sl <= 0xdfff) {
                        var cp = ((sl & 0x3ff) | ((sh & 0x3ff) << 10)) + 0x10000;
                        result[r++] = String.fromCodePoint(cp);
                        i += 4;
                    } else {
                        result[r++] = String.fromCodePoint(sh);
                        i += 2
                    }

                } else {
                    result[r++] = String.fromCodePoint(sh);
                    i += 2;
                }
            } else {
                var cp = array[i];
                if (cp < 0x80) {//1字节 0xxxxxxx//7
                    result[r++] = String.fromCodePoint(cp);
                    i += 1;
                } else if (cp < 0xE0) {//2字节 110xxxxx 10xxxxxx//11
                    result[r++] = String.fromCodePoint(((cp & 0x1f) << 6) | (array[i + 1] & 0x3f));
                    i += 2;
                } else if (cp < 0xF0) {//3字节 1110xxxx 10xxxxxx 10xxxxxx//16
                    result[r++] = String.fromCodePoint(((cp & 0x0f) << 12) | ((array[i + 1] & 0x3f) << 6) | (array[i + 2] & 0x3f));
                    i += 3;
                } else if (cp < 0xF8) {//4字节 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx//21
                    result[r++] = String.fromCodePoint(((cp & 0x7) << 18) | ((array[i + 1] & 0x3f) << 12) | ((array[i + 2] & 0x3f) << 6) | (array[i + 3] & 0x3f));
                    i += 4;
                } else if (cp < 0xFC) {//5字节 111110xx 10xxxxxx 10xxxxxx 10xxxxxx 10xxxxxx//26
                    result[r++] = String.fromCodePoint(((cp & 0x3) << 24) | ((array[i + 1] & 0x3f) << 18) | ((array[i + 2] & 0x3f) << 12) | ((array[i + 3] & 0x3f) << 6) | (array[i + 4] & 0x3f));
                    i += 5;
                } else /*if (cp < 0x80000000)*/ {//6字节 1111110x 10xxxxxx 10xxxxxx 10xxxxxx 10xxxxxx 10xxxxxx//31
                    result[r++] = String.fromCodePoint(((cp & 0x1) << 30) | ((array[i + 1] & 0x3f) << 24) | ((array[i + 2] & 0x3f) << 18) | ((array[i + 3] & 0x3f) << 12) | ((array[i + 4] & 0x3f) << 6) | (array[i + 5] & 0x3f));
                    i += 6;
                }
            }
        }
        return result.join('');
    }

    var utfToBytes = (str, type) => {
        var i = 0;
        var result = [];
        var r = 0;
        while (i < str.length) {
            var cp = str.codePointAt(i);
            if (typeof cp !== "number" || cp < 0 || cp > 0x10FFFF) {
                throw new TypeError("Invalid Code Point!");
            }
            if (cp > 0xffff) {
                if (str.codePointAt(i + 1) == ((cp - 0x10000) & 0x3FF | 0xDC00)) {//must here
                    i += 2;//maping
                } else {
                    throw new TypeError("Invalid Code Point!");
                    //cp = str.charCodeAt(i);
                    //i++;
                }
            } else {
                i++;
            }
            if (type == "utf32le") {
                result[r++] = cp & 0xff;
                result[r++] = (cp >> 8) & 0xff;
                result[r++] = (cp >> 16) & 0xff;
                result[r++] = (cp >> 24) & 0xff;
            } else if (type == "utf32be") {
                result[r++] = (cp >> 24) & 0xff;
                result[r++] = (cp >> 16) & 0xff;
                result[r++] = (cp >> 8) & 0xff;
                result[r++] = cp & 0xff;
            } else if (type == "utf16le") {//D800-DBFF   DC00-DFFF
                if (cp > 0xFFFF) {
                    cp -= 0x10000;
                    var sh = (cp >>> 10) | 0xD800;
                    var sl = (cp & 0x3FF) | 0xDC00;
                    result[r++] = sh & 0xff;
                    result[r++] = (sh >> 8) & 0xff;
                    result[r++] = sl & 0xff;
                    result[r++] = (sl >> 8) & 0xff;
                } else {
                    result[r++] = cp & 0xff;
                    result[r++] = (cp >> 8) & 0xff;
                }
            } else if (type == "utf16be") {
                if (cp > 0xFFFF) {
                    cp -= 0x10000;
                    var sh = (cp >>> 10) | 0xD800;
                    var sl = (cp & 0x3FF) | 0xDC00;
                    result[r++] = (sh >> 8) & 0xff;
                    result[r++] = sh & 0xff;
                    result[r++] = (sl >> 8) & 0xff;
                    result[r++] = sl & 0xff;
                } else {
                    result[r++] = (cp >> 8) & 0xff;
                    result[r++] = cp & 0xff;
                }
            } else {
                if (cp < 0x80) {//1字节 0xxxxxxx//7
                    result[r++] = cp;
                } else if (cp < 0x800) {//2字节 110xxxxx 10xxxxxx//11
                    result[r++] = (cp >>> 6) | 0xc0;
                    result[r++] = (cp & 0x3F) | 0x80;
                } else if (cp < 0x10000) {//3字节 1110xxxx 10xxxxxx 10xxxxxx//16
                    result[r++] = (cp >>> 12) | 0xe0;
                    result[r++] = ((cp >>> 6) & 0x3f) | 0x80;
                    result[r++] = cp & 0x3F | 0x80;
                } else if (cp < 0x200000) {//4字节 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx//21
                    result[r++] = (cp >>> 18) | 0xf0;
                    result[r++] = ((cp >>> 12) & 0x3f) | 0x80;
                    result[r++] = ((cp >>> 6) & 0x3f) | 0x80;
                    result[r++] = cp & 0x3f | 0x80;
                } else if (cp < 0x4000000) {//5字节 111110xx 10xxxxxx 10xxxxxx 10xxxxxx 10xxxxxx//26
                    result[r++] = (cp >>> 24) | 0xf8;
                    result[r++] = ((cp >>> 18) & 0x3f) | 0x80;
                    result[r++] = ((cp >>> 12) & 0x3f) | 0x80;
                    result[r++] = ((cp >>> 6) & 0x3f) | 0x80;
                    result[r++] = cp & 0x3f | 0x80;
                } else /*if (cp < 0x80000000)*/ {//6字节 1111110x 10xxxxxx 10xxxxxx 10xxxxxx 10xxxxxx 10xxxxxx//31
                    result[r++] = (cp >>> 30) | 0xfc;
                    result[r++] = ((cp >>> 24) & 0x3f) | 0x80;
                    result[r++] = ((cp >>> 18) & 0x3f) | 0x80;
                    result[r++] = ((cp >>> 12) & 0x3f) | 0x80;
                    result[r++] = ((cp >>> 6) & 0x3f) | 0x80;
                    result[r++] = cp & 0x3f | 0x80;
                }
            }
        }
        result.length = r;
        return result;
    }

    var bytesToHex = (arr, space) => {
        return [].map.call(arr, a => a.toString(16).padStart(2, 0)).join(space || '-');
    }

    var hexToBytes = str => {
        str = str.replace(/[^0-9a-fA-F]/g, '');
        var result = [];
        var r = 0;
        var len = str.length;
        for (var i = 0; i < len; i += 2) {
            result[r++] = parseInt(str.substr(i, 2), 16);
        }
        result.length = r;
        return result;
    }
    return {
        bytesToBase64,
        base64ToBytes,
        utfToBytes,
        bytesToUtf,
        bytesToHex,
        hexToBytes
    }
}();
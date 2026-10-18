!async function () {
    if (globalThis._yidong_) {
        globalThis._yidong_.showDownload();
        return;
    }
    const enc = new TextEncoder(),
        dec = new TextDecoder(),
        key = await crypto.subtle.importKey('raw', enc.encode('PVGDwmcvfs1uV3d1'), { name: 'AES-CBC' }, false, ['encrypt', 'decrypt']),
        bufToB64 = buf => btoa(String.fromCharCode(...new Uint8Array(buf))),
        b64ToBuf = b64 => Uint8Array.from(atob(b64.replace(/\s+/g, '')), c => c.charCodeAt(0)),
        _e = async plainObj => {
            const iv = crypto.getRandomValues(new Uint8Array(16));
            const ct = await crypto.subtle.encrypt({ name: 'AES-CBC', iv }, key, enc.encode(JSON.stringify(plainObj)));
            return bufToB64(new Uint8Array([...iv, ...new Uint8Array(ct)]));
        },
        _d = async (b64) => {
            if (!b64) return '';
            const buf = b64ToBuf(b64);
            const iv = buf.slice(0, 16);
            const ct = buf.slice(16);
            const pt = await crypto.subtle.decrypt({ name: 'AES-CBC', iv }, key, ct);
            return dec.decode(pt);
        },
        getCookie = (name) => {
            const match = globalThis.document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
            return match ? decodeURIComponent(match[1]) : null;
        },
        getShareDownloadUrl = async (linkID, path, account, authorization) => {
            const
                body = {
                    dlFromOutLinkReqV3: { account, linkID, coIDLst: { item: [path] } },
                    commonAccountInfo: { account, accountType: 1 }
                },
                raw = await (await fetch("https://share-kd-njs.yun.139.com/yun-share/richlifeApp/devapp/IOutLink/dlFromOutLinkV3", {
                    "headers": {
                        "authorization": authorization,
                        "content-type": "application/json;charset=UTF-8",
                        "hcy-cool-flag": "1",
                        "x-deviceinfo": "||9|12.27.0|chrome|155.0.0.0|||windows 10||zh-CN|||",
                        "x-yun-api-version": "v1",
                        "x-yun-app-channel": "10000034",
                        "x-yun-client-info": "||9|7.13.3|chrome|155.0.0.0|||windows 10||zh-CN|||||",
                    },
                    "body": await _e(body),
                    "method": "POST",
                    "credentials": "include"
                })).text(),
                result = JSON.parse(await _d(raw));
            return result.data.redrUrl;
        },
        getFileDownloadUrl = async (contentID, authorization) => {
            const
                body = { fileId: contentID },
                raw = await (await fetch("https://personal-kd-njs.yun.139.com/hcy/file/getDownloadUrl", {
                    "headers": {
                        "authorization": authorization,
                        "content-type": "application/json;charset=UTF-8",
                        "x-deviceinfo": "||9|12.27.0|chrome|155.0.0.0|||windows 10||zh-CN|||",
                        "x-yun-api-version": "v1",
                        "x-yun-app-channel": "10000034",
                        "x-yun-client-info": "||9|7.13.3|chrome|155.0.0.0|||windows 10||zh-CN|||||",
                    },
                    "body": JSON.stringify(body),
                    "method": "POST",
                    "credentials": "include"
                })).text(),
                result = JSON.parse(raw);
            return result.data.url;
        },

        getSelectedFileList = () => globalThis.document.querySelector(".main_file_list").__vue__.selectList,
        getSelectedLinkID = () => globalThis.document.querySelector(".main_file_list").__vue__.linkID,

        showDownload = async () => {
            const authorization = getCookie("authorization"),
                linkId = getSelectedLinkID(),
                fileList = getSelectedFileList();
            if (linkId) {
                const account = globalThis.MCloudVM.$store.state.auth.accountPhone;
                await Promise.all(fileList.map(async item => !item.item.coType && console.info(`文件：[${item.item.coName}] (${await getShareDownloadUrl(linkId, item.item.path, account, authorization)})`)));
            }
            else {
                await Promise.all(fileList.map(async item => !item.item.coType && console.info(`文件：[${item.item.contentName}] (${await getFileDownloadUrl(item.item.contentID, authorization)})`)));
            }
        };
    const yidong = globalThis._yidong_ = {
        key,
        enc,
        dec,
        bufToB64,
        b64ToBuf,
        _d,
        _e,
        getFileDownloadUrl,
        getSelectedFileList,
        showDownload
    }
    await yidong.showDownload();
}();

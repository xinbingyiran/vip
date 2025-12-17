!function () {
    if (globalThis._tianyi_) {
        globalThis._tianyi_.showDownload();
        return;
    }
    var tianyi = globalThis._tianyi_ = {};
    tianyi.addScript = (url, useCallback) => {
        var script = globalThis.document.createElement('script');
        script.setAttribute('type', 'text/javascript'), script.setAttribute('src', url), script.onload = useCallback, globalThis.document.getElementsByTagName('head')[0].appendChild(script);
    }
    tianyi.getSignature = e => window.CryptoJS.MD5(Object.entries(e).map(([k, v]) => `${k}=${v}`).toSorted().join("&")).toString();

    tianyi.getFileDownloadUrl = async function (fileId, shareId) {
        var accessToken = localStorage.getItem("accessToken").replace(/[\"\\]/g, "")
            , timestamp = Date.now()
            , signature = tianyi.getSignature({ AccessToken: accessToken, Timestamp: timestamp, fileId: fileId, ...shareId && { dt: 1, shareId: shareId } }),
            url = "https://api.cloud.189.cn/open/file/getFileDownloadUrl.action?fileId=" + fileId + (shareId ? "&dt=1&shareId=" + shareId : ""),
            opt = { headers: { Accept: "application/json;charset=UTF-8", AccessToken: accessToken, Signature: signature, "Sign-Type": 1, Timestamp: timestamp } };

        return (await (await fetch(url, opt)).json()).fileDownloadUrl;
    };

    tianyi.getSelectedFileList = function () {
        var lvue = globalThis.document.querySelector(".c-file-list")?.__vue__, fvue = globalThis.document.querySelector(".info-detail")?.__vue__;
        return lvue ? lvue.selectLength ? lvue.selectedList : lvue.fileList : fvue ? Object.keys(fvue.fileDetail).length ? [fvue.fileDetail] : [] : [];
    };

    tianyi.showDownload = async function () {
        var fileList = tianyi.getSelectedFileList();
        await Promise.all(fileList.map(async (item) => {
            item.isFolder ? void 0 : console.info(`文件：[${item.fileName ? item.fileName : item.fileId}] (${item.downloadUrl ?? await tianyi.getFileDownloadUrl(item.fileId, item.shareId)})`);
        }));
    };

    tianyi.addScript("https://cdnjs.cloudflare.com/ajax/libs/crypto-js/4.2.0/crypto-js.min.js", tianyi.showDownload);
}();

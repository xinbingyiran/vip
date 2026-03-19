!function () {
    if (globalThis._123_) {
        globalThis._123_.showDownload();
        return;
    }
    const _123 = globalThis._123_ = {};

    _123.getFileDownloadUrl = async function (authorization, item) {
        const content = JSON.stringify({ DriveId: 0, Etag: item.Etag, FileId: item.FileId, S3KeyFlag: item.S3KeyFlag, Type: 0, FileName: item.FileName, Size: item.Size }),
            url = "https://www.123pan.com/b/api/file/download_info",
            opt = { method: "POST", body: content, headers: { Authorization: authorization, "Content-Type": "application/json;charset=UTF-8", Accept: "application/json;charset=UTF-8" } };

        return (await (await fetch(url, opt)).json()).data.DownloadUrl;
    };

    _123.getReact = function (selector) {
        return Object.entries(document.querySelector(selector)).find(([key]) => key.startsWith("__reactFiber$") || key.startsWith("__reactInternalInstance$"))[1];
    }

    _123.getSelectedFileList = function () {
        const props = _123.getReact(".file-list-container").return.pendingProps,
            record = props.isSingleFile ? props.serverFileList : _123.getReact(".ant-table-wrapper").return.pendingProps.dataSource;
        return record;
    };

    _123.showDownload = async function () {
        const fileList = _123.getSelectedFileList(),
            authorToken = localStorage.getItem("authorToken").replace(/[\"\\]/g, ""),
            authorization = `Bearer ${authorToken}`;
        await Promise.all(fileList.map(async item => item.Etag && console.info(`文件：[${item.FileName ? item.FileName : item.FileId}] (${item.DownloadUrl ? item.DownloadUrl : await _123.getFileDownloadUrl(authorization, item)})`)));
    };

    _123.showDownload();
}();

!function () {
    if (globalThis._123_) {
        globalThis._123_.showDownload();
        return;
    }
    const _123 = globalThis._123_ = {};

    _123.getFileDownloadUrl = async function (authorization, item) {
        const content = JSON.stringify({
            DriveId: 0,
            Etag: item.Etag,
            FileId: item.FileId,
            S3KeyFlag: item.S3KeyFlag,
            Type: 0,
            FileName: item.FileName,
            Size: item.Size
        });
        const url = "https://www.123pan.com/b/api/file/download_info";
        opt = { method: "POST", body: content, headers: { "Content-Type": "application/json", Accept: "application/json;charset=UTF-8", Authorization: authorization } };

        return (await (await fetch(url, opt)).json()).data.DownloadUrl;
    };

    _123.getSelectedFileList = function () {
        const sitem = document.querySelector("div.single-file-sharing-container-content-file"),
            record = sitem ? [Object.values(sitem)[1].children[0].props.children.props.file] :
                Object.values(document.querySelector("div.web-file-list-container"))?.[1]?.children?.props?.children?.props?.dataSource;
        return record;
    };

    _123.showDownload = async function () {
        const fileList = _123.getSelectedFileList();
        const authorToken = localStorage.getItem("authorToken").replace(/[\"\\]/g, "");
        const authorization = `Bearer ${authorToken}`;
        await Promise.all(fileList.map(async item => item.Etag && console.info(`文件：[${item.FileName ? item.FileName : item.FileId}] (${item.DownloadUrl ? item.DownloadUrl : await _123.getFileDownloadUrl(authorization, item)})`)));
    };

    _123.showDownload();
}();

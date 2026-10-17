!function () {
    if (globalThis._baidu_) {
        globalThis._baidu_.showDownload();
        return;
    }
    const baidu = globalThis._baidu_ = {};

    baidu.getBdstoken = async () => (await (await fetch('https://pan.baidu.com/api/gettemplatevariable?clienttype=0&web=1&fields=' + encodeURIComponent('["bdstoken","uk"]'), {
        credentials: 'include'
    })).json()).result.bdstoken;

    baidu.getFileDownloadDatas = async function (items) {
        const url = `https://pan.baidu.com/api/filemetas?fsids=${JSON.stringify(items.map(s => s.fs_id))}&dlink=1&logid=M`,
            opt = { credentials: 'include' }
        return (await (await fetch(url, opt)).json()).info;
    };

    baidu.getSelectedFileList = () => globalThis.document.querySelector(".wp-s-pan-list")?.__vue__?.selectedList;

    baidu.showDownload = async function () {
        const fileList = baidu.getSelectedFileList().filter(s => !s.isdir);
        (await baidu.getFileDownloadDatas(fileList)).forEach(item => {
            console.info(`文件：[${item.server_filename}] (${item.dlink}) 【ua=pan.baidu.com】`);
        });
    };

    baidu.showDownload();
}();

!function () {
    if (globalThis._kuake_) {
        globalThis._kuake_.showDownload();
        return;
    }
    const kuake = globalThis._kuake_ = {};
    kuake.apiBase = "https://drive-pc.quark.cn";

    kuake.allProps = function* (selector) {
        const el = document.querySelector(selector);
        if (!el) return null;
        const entry = Object.entries(el).find(function (kv) { return kv[0].indexOf("__reactFiber$") === 0 || kv[0].indexOf("__reactInternalInstance$") === 0; });
        let fiber = entry ? entry[1] : null;
        while (fiber) { yield fiber.memoizedProps; fiber = fiber.return; }
    };

    kuake.getProps = function (selector, propsChecker) {
        const i = kuake.allProps(selector);
        return i.find(propsChecker);
    };

    kuake.dt = function () {
        const d = new Date();
        return d.getFullYear() + "-" + String(d.getMonth() + 1).padStart(2, "0") + "-" + String(d.getDate()).padStart(2, "0");
    };

    kuake.qs = function (extra) {
        return "pr=ucpro&fr=pc&uc_param_str=&__dt=" + kuake.dt() + "&__t=" + Date.now() + (extra ? "&" + extra : "");
    };

    kuake.getShareDownloadUrl = async function (stoken, pwdId, fid) {
        const r = await (await fetch(kuake.apiBase + "/1/clouddrive/file/share/download?" + kuake.qs(), {
            method: "POST",
            credentials: "include",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ fids: [fid], pwd_id: pwdId, stoken: stoken })
        })).json();
        if (r.status !== 200) return { error: r.code + " " + (r.message || "") };
        const data = r.data;
        const url = Array.isArray(data) ? data[0]?.download_url : data?.download_url;
        return url ? { url } : { error: "无 download_url" };
    };

    kuake.getHomeDownloadUrl = async function (fid) {
        const r = await (await fetch(kuake.apiBase + "/1/clouddrive/file/download?" + kuake.qs(), {
            method: "POST",
            credentials: "include",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ fids: [fid] })
        })).json();
        if (r.status !== 200) return { error: r.code + " " + (r.message || "") };
        const data = r.data;
        const url = Array.isArray(data) ? data[0]?.download_url : data?.download_url;
        return url ? { url } : { error: "无 download_url" };
    };

    kuake.walk = async function (items, path, stoken, pwdId, mode) {
        for (const item of items) {
            if (item.dir) {
                continue;
            }
        }
    };

    kuake.showDownload = async function () {
        if (document.querySelector(".section-main")) {
            const props = kuake.getProps(".ant-table-default", s => s?.rowSelection);
            if (!props) return;
            const rowKey = props.rowKey;
            const keys = props.rowSelection?.selectedRowKeys;
            await Promise.all(props.dataSource?.filter(s => !s.dir && keys.includes(s[rowKey])).map(async item => {
                const dl = await kuake.getHomeDownloadUrl(item.fid);
                if (dl.url) {
                    console.info(`文件：[${item.file_name}] (${dl.url})`);
                } else {
                    console.warn(`文件：[${item.file_name}] 获取下载链接失败: ${dl.error}`);
                }
            }));
        } else {
            const props = kuake.getProps(".ant-table-default", s => s?.rowSelection);
            if (!props) return;
            const sprops = kuake.getProps(".ant-table-default", s => s?.stoken);
            if (!sprops) return;
            const stoken = sprops.stoken;
            const pwdId = sprops.pwdid;
            const rowKey = props.rowKey;
            const keys = props.rowSelection?.selectedRowKeys;
            await Promise.all(props.dataSource?.filter(s => !s.dir && keys.includes(s[rowKey])).map(async item => {
                const dl = await kuake.getShareDownloadUrl(stoken, pwdId, item.fid);
                if (dl.url) {
                    console.info(`文件：[${item.file_name}] (${dl.url})`);
                } else {
                    console.warn(`文件：[${item.file_name}] 获取下载链接失败: ${dl.error}`);
                }
            }));
        }
    };

    kuake.showDownload();
}();

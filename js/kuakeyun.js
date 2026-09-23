!function () {
    if (globalThis._kuake_) {
        globalThis._kuake_.showDownload();
        return;
    }
    const kuake = globalThis._kuake_ = {};
    kuake.apiBase = "https://drive-pc.quark.cn";
    // kuake.RECURSIVE = false; // true=全量解析所有子目录; false=只解析当前目录（已注释：不再解析子目录）
    // 从 DOM 元素提取 React fiber（与 123yun.js 同一模式）
    kuake.getReact = function (selector) {
        const el = document.querySelector(selector);
        if (!el) return null;
        return Object.entries(el).find(function (kv) { return kv[0].indexOf("__reactFiber$") === 0 || kv[0].indexOf("__reactInternalInstance$") === 0; })?.[1] || null;
    };

    // 组件 props：fiber.return.pendingProps，缺省回退 memoizedProps
    kuake.getProps = function () {
        const fiber = kuake.getReact(".file-list");
        if (!fiber) return null;
        const node = fiber.return || fiber;
        return node.pendingProps || node.memoizedProps || null;
    };

    // 当前目录 fid：不解析地址栏，从 props.dirInfo 容错获取，缺省根目录 "0"
    kuake.getCurrentDirFid = function (props) {
        const d = props.dirInfo;
        if (d && typeof d === "object") {
            if (typeof d.fid === "string" && d.fid) return d.fid;
            if (typeof d.pdir_fid === "string" && d.pdir_fid) return d.pdir_fid;
        }
        return "0";
    };

    kuake.dt = function () {
        const d = new Date();
        return d.getFullYear() + "-" + String(d.getMonth() + 1).padStart(2, "0") + "-" + String(d.getDate()).padStart(2, "0");
    };

    kuake.qs = function (extra) {
        return "pr=ucpro&fr=pc&uc_param_str=&__dt=" + kuake.dt() + "&__t=" + Date.now() + (extra ? "&" + extra : "");
    };

    // 仅子目录递归 / 当前目录翻页补全时需要 detail API；当前页列表直接来自 React props
    kuake.listDir = async function (stoken, pwdId, pdirFid, page) {
        const url = kuake.apiBase + "/1/clouddrive/share/sharepage/detail?" + kuake.qs("pwd_id=" + pwdId + "&stoken=" + encodeURIComponent(stoken) + "&pdir_fid=" + pdirFid + "&force=0&_page=" + page + "&_size=50&_sort=file_type:asc,updated_at:desc");
        const r = await (await fetch(url, { credentials: "include" })).json();
        return r.status === 200 ? r.data : null;
    };

    kuake.getDownloadUrl = async function (stoken, pwdId, fid) {
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

    kuake.showDownload = async function () {
        const props = kuake.getProps();
        if (!props || !Array.isArray(props.list)) {
            console.error("未从 .file-list 的 React fiber 中获取到列表，请确认分享页文件已渲染");
            return;
        }
        const stoken = props.stoken;
        const pwdId = props.pwdid;
        if (!stoken || !pwdId) {
            console.error("pendingProps 中缺少 stoken/pwdid");
            return;
        }
        let curList = props.list.slice();
        const loaded = curList.length;
        const total = props.total || loaded;
        console.info(`夸克分享解析：pwd_id=${pwdId}，共 ${total} 项（DOM 已加载 ${loaded}）`);
        // DOM 无限滚动只渲染了首屏且 hasMore 时，用 detail API 翻页补全当前目录
        if (props.hasMore && total > loaded) {
            const pdirFid = kuake.getCurrentDirFid(props);
            let page = 2;
            while (curList.length < total) {
                const data = await kuake.listDir(stoken, pwdId, pdirFid, page);
                if (!data || !Array.isArray(data.list) || !data.list.length) break;
                curList = curList.concat(data.list);
                if (!data.has_more) break;
                page++;
            }
        }
        const walk = async function (items, path) {
            for (const item of items) {
                if (item.dir) {
                    // [已注释] 子目录解析已停用，只处理当前目录文件
                    // console.info(`[目录] ${path}${item.file_name}/`);
                    // if (kuake.RECURSIVE) {
                    //     const data = await kuake.listDir(stoken, pwdId, item.fid, 1);
                    //     if (data && Array.isArray(data.list)) await walk(data.list, `${path}${item.file_name}/`);
                    // }
                } else {
                    const dl = await kuake.getDownloadUrl(stoken, pwdId, item.fid);
                    if (dl.url) {
                        console.info(`文件：[${item.file_name}] (${dl.url})`);
                    } else {
                        console.warn(`文件：[${item.file_name}] 获取下载链接失败: ${dl.error}`);
                    }
                }
            }
        };
        await walk(curList, "");
    };

    kuake.showDownload();
}();

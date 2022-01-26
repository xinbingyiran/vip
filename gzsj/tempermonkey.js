// ==UserScript==
// @name         光之圣境脚本
// @namespace    http://tampermonkey.net/
// @version      0.1
// @description  光之圣境重复爬塔、跳过战斗、自动捡矿脚本
// @author       anonymous
// @match        https://idleherocdn2.hotgamehl.com/ver/fangzhi/t/*
// @icon         data:image/gif;base64,R0lGODlhAQABAAAAACH5BAEKAAEALAAAAAABAAEAAAICTAEAOw==
// @grant        none
// ==/UserScript==

'use strict';
!function (e) {
    var g, a, b, c = !1, r, w1, w2, ms, sc, gf, k, bt, bm, z = 0, x = 10,
        pt = function () {
            /*爬下一层塔*/
            a = new g.BtlNormalReqParam;
            a.mapId = g.HGActivityMapMgr.Ins.GetInfo(27).nMaxMap + 1;
            a.eventId = 0;
            g.BtlTypeMgr.Ins.ReqBtl(a);
        },
        cpt = function () {
            /*检测爬塔*/
            (b = bt.mInfo) && (b.base_Wnd == 4) && (b.base_Type == 9) && (!b.base_bHelp) && (bt.SimpleClose(), pt())
        },
        cw1 = function () {
            /*关闭资源窗口*/
            w1 || (w1 = g.WndOfflineIncome && g.WndOfflineIncome.Ins);
            w1 && w1.mIsShow && w1.SimpleClose();
        },
        cw2 = function () {
            /*关闭获取窗口*/
            w2 || (w2 = g.WndFlyGetItem2 && g.WndFlyGetItem2.Ins);
            w2 && w2.mIsShow && w2.SimpleClose();
        },
        yz = function () {
            //远征关卡
            ms || (ms = g.HGMSExpeditionMgr && g.HGMSExpeditionMgr.Ins);
            ms && ms.GetUserInfo(1) && ms.GetUserInfo(1).GetBoxBuyCount(4) && ms.GetUserInfo(2).curMap < 15 && (ms.GetUserInfo(2).curMap = 15);
        },
        ct = function () {
            /*战斗结束窗口显示时间*/
            bt || (bt = g.WndBattleRes && g.WndBattleRes.Ins);
            bt && (bt._win_auto_close_time = bt._failed_auto_close_time = 1e3);
        },
        jl = function () {
            /*自动捡周年庆礼物*/
            sc || (sc = g.s_config && g.s_config.Ins);
            gf || (gf = g.HgActNpcGiftMgr && g.HgActNpcGiftMgr.Ins);
            gf && (a = gf.mNpcInfo) && a.u32RecvNum < sc.QueryVal("NPC_ONE_GIFT_NUM")
                && sc && r.Query(100000237, 0, 1) < sc.QueryVal("NPC_PERSON_DAY_NUM")
                && g.Msgh5new.ActNpcClickReq().send();
        },
        jk = function () {
            /*自动捡矿*/
            k || (k = g.KuangBattleMgr && g.KuangBattleMgr.Ins);
            k && k.GetReachStone(r.Query(200000002, 0))
                && g.MsgH5SG2.CreateUserRecvOreveinStoneReq().send();
        },
        tj = function () {
            /*跳过战斗*/
            (bm || (bm = g.BattleMgr && g.BattleMgr.Ins)) && (a = bm.GetBtlType()) && (a = a.GetPhaFight()) && a.EvtMgr.Skip();
        },
        ck = function () {
            r || (r = g.HGComAttrMgr && g.HGComAttrMgr.Ins);
            bt || (bt = g.WndBattleRes && g.WndBattleRes.Ins);
            z++; cw1(); cw2(); yz();
            r && (z % x == 0) && (jl(), jk(), ct());
            bt && bt.mIsShow ? cpt() : tj();
        },
        f = function () {
            g || (g = window.hg && window.hg.game);
            g && ck();
        };
    e.t && clearInterval(e.t);
    e.t = setInterval(f, 1e3);
}(window._g || (window._g = {}));
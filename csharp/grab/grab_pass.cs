#:property Nullable=disable
#:property ImplicitUsings=enable
// ============================================================================
// NativeAOT 口令批量解码器·一体版（.NET 10 file-based app，零第三方依赖）
// 用法: dotnet run NativeAOT_口令批量解码器.cs <目录或exe路径> [名称过滤词...]
//   - 目录模式: 递归遍历所有下级目录，处理名称匹配的 exe（默认 kaishiyouxi 前缀）
//   - 文件模式: 直接处理单个 exe，不需要任何 txt
//   - 旧用法 <exe> <定位txt> 仍兼容（种子改从 txt 读取）
// 流程: 内联种子定位(String/byte[] MT投票+分层排名) -> lea引用扫描(字节预筛+内置x86-64迷你反汇编精读)
//       -> PMAP/KMAP/Array.Copy 三种派生形态还原 -> 冲突槽位组合枚举
//       -> AES-256-CBC 暴力配对 -> PKCS7+可打印验收 -> 控制台直接输出双口令+汇总
// ============================================================================
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = Encoding.UTF8;

// ---- 参数与目标收集: 目录递归 / 单文件 / 兼容旧 exe+txt 用法 ----
string target = args.Length > 0 ? args[0] : ".";
string legacyTxt = args.Length >= 2 && File.Exists(args[0]) && File.Exists(args[1]) &&
				   args[1].EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? args[1] : null;
string[] pats = (legacyTxt == null ? args.Skip(1) : args.Skip(2))
				.Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
if (pats.Length == 0) pats = new[] { "kaishiyouxi" };

var files = new List<string>();
if (File.Exists(target) || legacyTxt != null)
	files.Add(target);
else if (Directory.Exists(target))
{
	var eo = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
	files = Directory.EnumerateFiles(target, "*.exe", eo)
					 .Where(f => NameMatch(Path.GetFileName(f), pats))
					 .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
}
else { Console.WriteLine($"路径不存在: {target}"); return 1; }

if (files.Count == 0) { Console.WriteLine($"[{target}] 未找到匹配的 exe（名称过滤词: {string.Join('/', pats)}）"); return 1; }
Console.WriteLine($"[*] 目标 {files.Count} 个（名称过滤词: {string.Join('/', pats)}）");

var results = new List<(string file, List<string> plains)>();
foreach (var f in files)
{
	Console.WriteLine($"\n========== {f} ==========");
	try { results.Add((f, RunOne(f, legacyTxt))); }
	catch (Exception e) { Console.WriteLine($"[!] 失败: {e.Message}"); results.Add((f, null)); }
}

Console.WriteLine("\n========== 汇总 ==========");
foreach (var (f, plains) in results)
{
	string s = plains != null && plains.Count > 0 ? string.Join("   |   ", plains) : "（未解出）";
	Console.WriteLine($"  {f}  ->  {s}");
}
int ok = results.Count(r => r.plains != null && r.plains.Count > 0);
Console.WriteLine($"[*] 共 {files.Count} 个目标: 成功 {ok}，失败 {files.Count - ok}");
return 0;

// ============================ 单文件主流程 ============================
List<string> RunOne(string exePath, string locFile)
{
	var plains = new List<string>();
	var d = File.ReadAllBytes(exePath);
	var (img, secs) = ParsePe(d);
	var tsec = secs.First(s => s.name == ".text");
	var code = new byte[tsec.rsize];
	Array.Copy(d, (int)tsec.raddr, code, 0, (int)tsec.rsize);
	long tbase = img + tsec.vaddr;

	var seeds = locFile != null ? ReadLoc(locFile) : LocateSeeds(d, secs, img);
	if (seeds.Count == 0) { Console.WriteLine("未找到种子（非 NativeAOT 启动器或结构不同）"); return plains; }
	var seedByVa = new Dictionary<long, (int sn, int ln, byte[] hex)>();
	for (int i = 0; i < seeds.Count; i++) seedByVa[seeds[i].va] = (i + 1, seeds[i].ln, seeds[i].hex);
	long lo = seeds.Min(s => s.va), hi = seeds.Max(s => s.va) + seeds.Max(s => s.ln);
	Console.WriteLine($"[*] 种子 {seeds.Count} 个，VA 范围 0x{lo:X}-0x{hi:X}");

	// ---- lea 字节预筛（REX 感知） ----
	var leas = new List<(int addr, int regIdx, long va)>();
	for (int i = 0; i + 7 <= code.Length; i++)
	{
		int b0 = code[i];
		if ((b0 & 0xF0) != 0x40 || code[i + 1] != 0x8D || (code[i + 2] & 0xC7) != 0x05) continue;
		int disp = BitConverter.ToInt32(code, i + 3);
		long va = tbase + i + 7 + disp;
		if (seedByVa.ContainsKey(va))
			leas.Add((i, (((b0 >> 2) & 1) << 3) | ((code[i + 2] >> 3) & 7), va));
	}
	Console.WriteLine($"[*] 指向种子的 lea 指令：{leas.Count} 处");

	var PMAP = new List<(int dst, int sn, int idx, int la)>();
	var KMAP = new List<(int dst, int sn, int idx, int poff, int la)>();
	var COPIES = new List<(int sn, int srcIdx, int count, int dstOff, int la)>();
	var pbaseVotes = new Dictionary<string, int>();
	var dstVotes = new Dictionary<string, int>();

	for (int li = 0; li < leas.Count; li++)
	{
		var (la, regIdx, va) = leas[li];
		string reg = RegName(regIdx, 8);
		int sn = seedByVa[va].sn;

		// 从 lea 起线性解码最多 24 条指令
		var ins = new List<Ins>();
		int pos = la;
		for (int k = 0; k < 24 && pos < code.Length; k++)
		{
			var one = Dec(code, pos);
			if (one == null) break;
			ins.Add(one);
			pos += one.Len;
		}
		// ---- 形态A: lea rA,[seed]; movzx rB, byte [rA+off]; ... ----
		if (ins.Count >= 2 && ins[1].Mn == "movzx" && ins[1].RmBase == regIdx && !ins[1].RmIsRip)
		{
			int sidx = (int)ins[1].RmDisp - 0x10;
			string seedReg = RegName(ins[1].Reg1, 4);
			bool xorSeen = false; (string rb, int poff)? pRead = null; int dst = -1; string dstBase = null;
			for (int k = 2; k < ins.Count; k++)
			{
				var t = ins[k];
				if (t.Mn == "lea" && t.RmIsRip) break;                    // 下一条目的 lea
				if ((t.Mn == "movzx" || t.Mn == "movsx") && !t.RmIsRip && t.RmBase >= 0 &&
					t.RmBase != regIdx && pRead == null)
					pRead = (RegName(t.RmBase, 8), (int)t.RmDisp);
				if (t.Mn == "xor")
				{
					string n1 = t.Reg1 >= 0 ? RegName(t.Reg1, 4) : null;
					string n2 = t.Reg2 >= 0 ? RegName(t.Reg2, 4) : null;
					if (n1 == seedReg || n2 == seedReg) xorSeen = true;   // 真混合 XOR
				}
				if (t.Mn == "mov" && !t.RmIsRip && t.RmBase >= 0 && t.StoreSize == 1)
				{
					dst = (int)t.RmDisp; dstBase = RegName(t.RmBase, 8); break;
				}
			}
			if (dst < 0) continue;
			if (xorSeen && pRead != null)
			{
				KMAP.Add((dst, sn, sidx, pRead.Value.poff, la));
				pbaseVotes[pRead.Value.rb] = pbaseVotes.GetValueOrDefault(pRead.Value.rb) + 1;
			}
			else if (!xorSeen)
			{
				PMAP.Add((dst, sn, sidx, la));
			}
			dstVotes[dstBase] = dstVotes.GetValueOrDefault(dstBase) + 1;
		}
		// ---- 形态B: Array.Copy —— lea rcx,[seed]; ... call helper ----
		else if (regIdx == 1)   // rcx
		{
			int? callTarget = null; int edx = -1, r9d = -1;
			foreach (var t in ins.Skip(1))
			{
				if (t.Mn == "call") { callTarget = 1; break; }
				if (t.Mn == "xor" && t.Reg1 == t.Reg2 && t.Reg1 == 2) edx = 0;         // xor edx,edx
				if (t.Mn == "mov" && t.Reg1 == 2 && t.ImmSrc) edx = (int)t.Imm;        // mov edx,imm
				if (t.Mn == "xor" && t.Reg1 == t.Reg2 && t.Reg1 == 9) r9d = 0;         // xor r9d,r9d
				if (t.Mn == "mov" && t.Reg1 == 9 && t.ImmSrc) r9d = (int)t.Imm;        // mov r9d,imm
			}
			if (callTarget != null)
			{
				// 反向找 mov dword ptr [rsp+0x20], imm32 （count 在 lea 之前设置）
				int sp20 = -1;
				var pat = new byte[] { 0xC7, 0x44, 0x24, 0x20 };
				for (int back = 8; back <= 72 && la - back >= 0; back++)
				{
					int j = la - back;
					if (j + 8 <= code.Length &&
						code[j] == pat[0] && code[j + 1] == pat[1] && code[j + 2] == pat[2] && code[j + 3] == pat[3])
					{ sp20 = BitConverter.ToInt32(code, j + 4); break; }
				}
				if (sp20 >= 0)
					COPIES.Add((sn, Math.Max(edx, 0), sp20, Math.Max(r9d, 0), la));
			}
		}
	}
	Console.WriteLine($"[*] PMAP 条目={PMAP.Count}  KMAP 条目={KMAP.Count}  COPIES={COPIES.Count}  密文/IV 配对=暴力枚举");
	if (pbaseVotes.Count > 0) Console.WriteLine($"[*] P 数组基寄存器投票: {Fmt(pbaseVotes)}");
	if (dstVotes.Count > 0) Console.WriteLine($"[*] 目标数组基寄存器投票: {Fmt(dstVotes)}");

	var sm = new Dictionary<int, byte[]>();
	foreach (var (va, ln, hx) in seeds) sm[seedByVa[va].sn] = hx;

	// ---- 组装 P ----
	var P = new byte?[32];
	foreach (var (dst, sn, idx, _) in PMAP)
	{
		try { P[dst - 0x10] = sm[sn][idx]; }
		catch (Exception e) { Console.WriteLine($"  PMAP异常 {dst},{sn},{idx}: {e.Message}"); }
	}
	foreach (var (sn, srcIdx, count, dstOff, _) in COPIES)
		for (int k = 0; k < count; k++)
			if (dstOff + k >= 0 && dstOff + k < 32) P[dstOff + k] = sm[sn][srcIdx + k];   // 元素索引

	// ---- 组装 K（按槽位去重；分支槽位记为候选） ----
	var K = new byte?[32];
	var kdup = new Dictionary<int, List<(int sn, int idx, int poff)>>();
	foreach (var (dst, sn, idx, poff, _) in KMAP)
	{
		var key = (sn, idx, poff);
		if (!kdup.TryGetValue(dst, out var lst)) kdup[dst] = lst = new();
		if (!lst.Contains(key)) lst.Add(key);
	}
	var conflicts = new List<(int dst, List<(int sn, int idx, int poff)> cands)>();
	foreach (var (dst, lst) in kdup.OrderBy(kv => kv.Key))
	{
		try
		{
			byte pv = P[lst[0].poff - 0x10] ?? throw new Exception("P未就绪");
			K[dst - 0x10] = (byte)(sm[lst[0].sn][lst[0].idx] ^ pv);
			if (lst.Count > 1) conflicts.Add((dst, lst));
		}
		catch (Exception e) { Console.WriteLine($"  KMAP异常 dst={dst}: {e.Message}"); }
	}
	if (conflicts.Count > 0)
		Console.WriteLine($"[*] 条件分支槽位 {conflicts.Count} 个: [{string.Join(',', conflicts.Select(c => c.dst))}] (解密时暴力枚举)");

	var missP = Enumerable.Range(0, 32).Where(i => !P[i].HasValue).ToList();
	var missK = Enumerable.Range(0, 32).Where(i => !K[i].HasValue).ToList();
	Console.WriteLine($"[*] P 缺口=[{string.Join(',', missP)}]  K 缺口=[{string.Join(',', missK)}]");
	if (missP.Count > 0 || missK.Count > 0) { Console.WriteLine("[!] 映射不完整，中止"); return plains; }

	var Pb = P.Select(v => v.Value).ToArray();
	var Kb = K.Select(v => v.Value).ToArray();
	Console.WriteLine($"[*] P  = {Hex(Pb)}");
	Console.WriteLine($"[*] K  = {Hex(Kb)}  (32 字节)");

	// ---- 暴力配对解密（冲突槽位做组合枚举） ----
	var s16 = sm.Where(kv => kv.Value.Length == 16).OrderBy(kv => kv.Key).ToList();
	Console.WriteLine($"[*] 16字节种子：[{string.Join(',', s16.Select(kv => kv.Key))}]");

	var combos = new List<Dictionary<int, byte>> { new() };
	foreach (var (dst, cands) in conflicts)
	{
		var next = new List<Dictionary<int, byte>>();
		foreach (var combo in combos)
			foreach (var (sn, idx, poff) in cands)
			{
				try
				{
					var c2 = new Dictionary<int, byte>(combo);
					c2[dst - 0x10] = (byte)(sm[sn][idx] ^ Pb[poff - 0x10]);
					next.Add(c2);
				}
				catch { }
			}
		combos = next;
		if (combos.Count > 4096) break;
	}
	if (combos.Count > 1) Console.WriteLine($"[*] 冲突组合数 {combos.Count}，逐一验证…");

	var found = new HashSet<string>();
	foreach (var (ci, ov) in combos.Select((c, i) => (i, c)))
	{
		var kc = new byte[32];
		for (int i = 0; i < 32; i++) kc[i] = ov.TryGetValue(i, out var v) ? v : Kb[i];
		foreach (var (ctn, ct) in s16)
			foreach (var (ivn, iv) in s16)
			{
				if (ctn == ivn) continue;
				byte[] pt;
				try { pt = DecryptCbc(ct, kc, iv); } catch { continue; }
				byte pad = pt[^1];
				if (pad < 1 || pad > 16 || !pt.Skip(pt.Length - pad).All(b => b == pad)) continue;
				var core = pt[..(pt.Length - pad)];
				if (core.All(b => b is >= 32 and < 127))
				{
					string pw = Encoding.ASCII.GetString(core);
					string tag = combos.Count > 1 ? $"[组合{ci}] " : "";
					string line = $"{tag}明文: 密文=种子#{ctn} IV=种子#{ivn} -> \"{pw}\"";
					if (found.Add(line)) { Console.WriteLine($"[+] {line}"); plains.Add(pw); }
				}
			}
	}
	if (found.Count == 0)
	{
		Console.WriteLine("[!] 未得到可读明文，尝试 K/P 切半作为密钥…");
		foreach (var (nm, kc) in new[] { ("K[:32]", Kb), ("P[:32]", Pb) })
			foreach (var (ctn, ct) in s16)
				foreach (var (ivn, iv) in s16)
				{
					byte[] pt;
					try { pt = DecryptCbc(ct, kc, iv); } catch { continue; }
					byte pad = pt[^1];
					if (pad < 1 || pad > 16 || !pt.Skip(pt.Length - pad).All(b => b == pad)) continue;
					var core = pt[..(pt.Length - pad)];
					if (core.All(b => b is >= 32 and < 127))
					{
						string pw = Encoding.ASCII.GetString(core);
						Console.WriteLine($"[+] {nm}: 密文=种子#{ctn} IV=种子#{ivn} -> \"{pw}\"");
						plains.Add(pw);
					}
				}
	}
	return plains;
}

static string RegName(int idx, int size)
{
	string[] R64 = { "rax", "rcx", "rdx", "rbx", "rsp", "rbp", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15" };
	string[] R32 = { "eax", "ecx", "edx", "ebx", "esp", "ebp", "esi", "edi", "r8d", "r9d", "r10d", "r11d", "r12d", "r13d", "r14d", "r15d" };
	return size switch
	{
		64 => R64[idx & 15],
		32 => R32[idx & 15],
		8 => (idx & 15) < 8 ? new[] { "al", "cl", "dl", "bl", "ah", "ch", "dh", "bh" }[idx & 7]
							: new[] { "r8b", "r9b", "r10b", "r11b", "r12b", "r13b", "r14b", "r15b" }[idx & 7],
		_ => "?"
	};
}

static Ins? Dec(byte[] c, int pos)
{
	int p = pos;
	bool w64 = false;
	while (p < c.Length)                     // 传统前缀
	{
		int b0 = c[p];
		if (b0 is 0x2E or 0x3E or 0x26 or 0x36 or 0x64 or 0x65 or 0x67 or 0xF0 or 0xF2 or 0xF3) { p++; continue; }
		if (b0 == 0x66) { p++; continue; }   // 66 前缀（本场景按 32 位处理足够）
		break;
	}
	if (p >= c.Length) return null;
	int rex = 0;
	if (c[p] >= 0x40 && c[p] <= 0x4F) { rex = c[p]; p++; }
	if (p >= c.Length) return null;
	w64 = ((rex >> 3) & 1) != 0;
	int rr = (rex >> 2) & 1, bb = rex & 1;
	int op = c[p]; p++;

	(int mod, int reg, int rm, int baseReg, long disp, bool rip, int len) ModRm(int q)
	{
		int m = c[q];
		int mod = (m >> 6) & 3;
		int reg = (((m >> 3) & 7) | (rr << 3)) & 15;
		int rm = ((m & 7) | (bb << 3)) & 15;
		int len = 1; long disp = 0; int baseReg = -1; bool rip = false;
		if (mod == 3) return (mod, reg, rm, -1, 0, false, 1);
		if (rm == 4)
		{
			int s = c[q + 1]; len = 2;
			int bas = ((s & 7) | (bb << 3)) & 15;
			if (bas == 5 && mod == 0) { disp = BitConverter.ToInt32(c, q + 2); len += 4; }
			else
			{
				baseReg = bas;
				disp = mod == 1 ? (sbyte)c[q + 2] : mod == 2 ? BitConverter.ToInt32(c, q + 2) : 0;
				len += mod == 1 ? 1 : mod == 2 ? 4 : 0;
			}
		}
		else if (rm == 5 && mod == 0) { rip = true; disp = BitConverter.ToInt32(c, q + 1); len += 4; }
		else
		{
			baseReg = rm;
			disp = mod == 1 ? (sbyte)c[q + 1] : mod == 2 ? BitConverter.ToInt32(c, q + 1) : 0;
			len += mod == 1 ? 1 : mod == 2 ? 4 : 0;
		}
		return (mod, reg, rm, baseReg, disp, rip, len);
	}

	var ins = new Ins();
	int modLen(int q) { var r = ModRm(q); return r.len; }

	switch (op)
	{
		case 0x8D: // lea r64, m
			{
				var r = ModRm(p);
				ins.Mn = "lea"; ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp;
				ins.RmIsRip = r.rip; ins.Len = (p - pos) + r.len; return ins;
			}
		case 0x0F:
			{
				if (p >= c.Length) return null;
				int op2 = c[p]; p++;
				if (op2 is 0xB6 or 0xB7 or 0xBE)
				{
					var r = ModRm(p);
					ins.Mn = op2 == 0xBE ? "movsx" : "movzx";
					ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
					ins.Len = (p - pos) + r.len; return ins;
				}
				if (op2 >= 0x80 && op2 <= 0x8F)
				{
					ins.Mn = "jcc"; ins.RelTarget = BitConverter.ToInt32(c, p);
					ins.Len = (p - pos) + 4; return ins;
				}
				if (op2 >= 0x40 && op2 <= 0x4F)   // cmovcc —— 条件分支形态常见，需正确跨过
				{
					var r = ModRm(p);
					ins.Mn = "cmov"; ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
					ins.Len = (p - pos) + r.len; return ins;
				}
				if (op2 >= 0x90 && op2 <= 0x9F)   // setcc
				{
					var r = ModRm(p);
					ins.Mn = "setcc"; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
					ins.Len = (p - pos) + r.len; return ins;
				}
				if (op2 is 0x1F or 0xAF or 0x10 or 0x11 or 0x28 or 0x29 or 0x2E or 0x2F or 0x6F or 0x7F)
				{
					var r = ModRm(p);
					ins.Mn = op2 == 0x1F ? "nop" : "?";
					ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
					ins.Len = (p - pos) + r.len; return ins;
				}
				return null;
			}
		case 0x88: // mov rm8, r8（写入内存）
		case 0x89: // mov rm, r32/64
			{
				var r = ModRm(p);
				ins.Mn = "mov"; ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
				ins.StoreSize = op == 0x88 ? 1 : w64 ? 8 : 4;
				ins.Len = (p - pos) + r.len; return ins;
			}
		case 0x8A:
		case 0x8B: // mov r8/r32, rm
			{
				var r = ModRm(p);
				ins.Mn = "mov"; ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
				ins.Len = (p - pos) + r.len; return ins;
			}
		case 0xC6:
		case 0xC7: // mov rm, imm
			{
				var r = ModRm(p);
				if (r.reg != 0) return null;
				ins.Mn = "mov"; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
				ins.StoreSize = op == 0xC6 ? 1 : 4;
				if (op == 0xC6) { ins.Imm = (sbyte)c[p + r.len]; ins.Len = (p - pos) + r.len + 1; }
				else { ins.Imm = BitConverter.ToInt32(c, p + r.len); ins.Len = (p - pos) + r.len + 4; }
				return ins;
			}
		case 0x30:
		case 0x31:
		case 0x32:
		case 0x33: // xor
			{
				var (mod, reg, rm, baseReg, disp, rip, len) = ModRm(p);
				ins.Mn = "xor";
				ins.Reg1 = reg;
				if (mod == 3) ins.Reg2 = rm;           // reg,reg
				else { ins.RmBase = baseReg; ins.RmDisp = disp; ins.RmIsRip = rip; }
				ins.Len = (p - pos) + len; return ins;
			}
		case 0x38:
		case 0x39:
		case 0x3A:
		case 0x3B:
		case 0x00:
		case 0x01:
		case 0x02:
		case 0x03:
		case 0x08:
		case 0x09:
		case 0x0A:
		case 0x0B:
		case 0x28:
		case 0x29:
		case 0x2A:
		case 0x2B:
		case 0x84:
		case 0x85:
			{
				var r = ModRm(p);
				ins.Mn = "?"; ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
				ins.Len = (p - pos) + r.len; return ins;
			}
		case 0x83:
		case 0x81: // group1 r, imm
			{
				var r = ModRm(p);
				string[] names = { "add", "or", "adc", "sbb", "and", "sub", "xor", "cmp" };
				ins.Mn = names[r.reg];
				ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
				if (op == 0x83) { ins.Imm = (sbyte)c[p + r.len]; ins.ImmSrc = true; ins.Len = (p - pos) + r.len + 1; }
				else { ins.Imm = BitConverter.ToInt32(c, p + r.len); ins.ImmSrc = true; ins.Len = (p - pos) + r.len + 4; }
				return ins;
			}
		case >= 0xB8 and <= 0xBF: // mov r32/r64, imm
			{
				ins.Mn = "mov"; ins.Reg1 = ((op & 7) | (bb << 3)) & 15; ins.ImmSrc = true;
				if (w64) { ins.Imm = BitConverter.ToInt64(c, p); ins.Len = (p - pos) + 8; }
				else { ins.Imm = BitConverter.ToInt32(c, p); ins.Len = (p - pos) + 4; }
				return ins;
			}
		case 0xE8: // call rel32
			{
				ins.Mn = "call"; ins.RelTarget = BitConverter.ToInt32(c, p);
				ins.Len = (p - pos) + 4; return ins;
			}
		case 0xE9: { ins.Mn = "jmp"; ins.Len = (p - pos) + 4; return ins; }
		case 0xEB: { ins.Mn = "jmp"; ins.Len = (p - pos) + 1; return ins; }
		case >= 0x70 and <= 0x7F: { ins.Mn = "jcc"; ins.Len = (p - pos) + 1; return ins; }
		case 0xCC: case 0xC3: case 0xC9: { ins.Mn = "stop"; ins.Len = (p - pos) + 1; return ins; }
		case 0xFF: // group5: call/jmp r/m
			{
				var r = ModRm(p);
				ins.Mn = r.reg == 2 ? "call" : r.reg == 4 ? "jmp" : "?";
				ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
				ins.Len = (p - pos) + r.len; return ins;
			}
		case 0x63:
		case 0x8C:
		case 0x8E: // movsxd / seg
			{
				var r = ModRm(p);
				ins.Mn = "?"; ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
				ins.Len = (p - pos) + r.len; return ins;
			}
		case 0xF6:
		case 0xF7: // group3
			{
				var r = ModRm(p);
				ins.Mn = "?"; ins.Reg1 = r.reg; ins.RmBase = r.baseReg; ins.RmDisp = r.disp; ins.RmIsRip = r.rip;
				int extra = r.reg <= 1 ? (op == 0xF6 ? 1 : 4) : 0;
				ins.Len = (p - pos) + r.len + extra; return ins;
			}
		case 0x98: case 0x99: { ins.Mn = "ext"; ins.Len = (p - pos) + 1; return ins; }
		case >= 0x50 and <= 0x5F: { ins.Mn = "push/pop"; ins.Len = (p - pos) + 1; return ins; }
		default: return null;
	}
}
// ============================ 工具 ============================
static byte[] DecryptCbc(byte[] ct, byte[] key, byte[] iv)
{
	using var aes = Aes.Create();
	aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.None;
	using var dec = aes.CreateDecryptor();
	return dec.TransformFinalBlock(ct, 0, ct.Length);
}

static List<(long va, int ln, byte[] hex)> ReadLoc(string path)
{
	var raw = File.ReadAllBytes(path);
	string t = raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE ? Encoding.Unicode.GetString(raw)
			 : raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF ? new UTF8Encoding(false).GetString(raw, 3, raw.Length - 3)
			 : Encoding.UTF8.GetString(raw);
	var rx = new Regex(@"0x([0-9A-Fa-f]{6,9})\s+VA 0x([0-9A-Fa-f]+)\s+len=(\d+)\s+([0-9a-f]+)");
	var outp = new List<(long, int, byte[])>();
	foreach (Match m in rx.Matches(t))
	{
		int ln = int.Parse(m.Groups[3].Value);
		if (m.Groups[4].Value.Length != ln * 2) continue;
		outp.Add((Convert.ToInt64(m.Groups[2].Value, 16), ln, Convert.FromHexString(m.Groups[4].Value)));
	}
	return outp;
}

static string Fmt(Dictionary<string, int> d) =>
	"{" + string.Join(", ", d.Select(kv => $"'{kv.Key}': {kv.Value}")) + "}";

static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();

// ============================ 名称过滤 ============================
static bool NameMatch(string fileName, string[] pats)
{
	if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
	string stem = fileName.Substring(0, fileName.Length - 4).TrimEnd();
	foreach (var p in pats)
	{
		string pp = p.TrimEnd();
		if (stem.Equals(pp, StringComparison.OrdinalIgnoreCase)) return true;
		if (stem.StartsWith(pp + " ", StringComparison.OrdinalIgnoreCase)) return true;
		if (stem.StartsWith(pp + "(", StringComparison.OrdinalIgnoreCase)) return true;
	}
	return false;
}

// ============================ 内联种子定位（源自 NativeAOT_种子定位器） ============================
static List<(long va, int ln, byte[] hex)> LocateSeeds(byte[] d, List<(string name, uint vaddr, uint vsize, uint raddr, uint rsize)> secs, long img)
{
	var sec = secs.FirstOrDefault(s => s.name == ".data");
	if (sec.name == null) sec = secs.OrderByDescending(s => s.rsize).First();
	long lo = sec.raddr, hi = sec.raddr + sec.rsize;
	bool MtOk(ulong mt) => mt >= (ulong)img && Va2Ofs(secs, img, (long)mt) >= 0;

	// 第 1 步: String MT 投票（UTF-16 文本锚点）
	var votes = new Dictionary<ulong, int>();
	for (long o = lo; o + 16 <= hi; o += 4)
	{
		uint ln = U32(d, (int)o + 8);
		if (ln < 1 || ln > 4096 || o + 12 + 2 * ln > hi) continue;
		if (LooksUtf16(d, (int)o + 12, (int)(2 * ln)))
		{
			ulong mt = U64(d, (int)o);
			if (MtOk(mt)) votes[mt] = votes.GetValueOrDefault(mt) + 1;
		}
	}
	if (votes.Count == 0) throw new Exception("未找到 UTF-16 字符串锚点（可能加壳或非 NativeAOT）");
	var strMt = votes.OrderByDescending(kv => kv.Value).First().Key;

	// 第 2 步: byte[] 候选（pad==0 + 长度合理）
	var cands = new List<(int ofs, ulong mt, int ln, byte[] data)>();
	var votes2 = new Dictionary<ulong, int>();
	for (long o = lo; o + 16 <= hi; o += 4)
	{
		ulong mt = U64(d, (int)o);
		if (!MtOk(mt) || mt == strMt) continue;
		uint ln = U32(d, (int)o + 8);
		uint pad = U32(d, (int)o + 12);
		if (ln < 1 || ln > 8192 || pad != 0 || o + 16 + ln > hi) continue;
		var data = new byte[ln];
		Array.Copy(d, (int)o + 16, data, 0, (int)ln);
		if (data.All(b => b == 0)) continue;
		cands.Add(((int)o, mt, (int)ln, data));
		votes2[mt] = votes2.GetValueOrDefault(mt) + 1;
	}
	if (votes2.Count == 0) throw new Exception("未找到 byte[] 候选对象");

	// 第 3 步: 分层排名选定 byte[] MethodTable
	var stats = new Dictionary<ulong, (int n, int lmin, int lmax, double ent, int zr, int kinds)>();
	foreach (var mt in votes2.Keys)
	{
		var grp = cands.Where(c => c.mt == mt).ToList();
		var lens = grp.Select(g => g.ln).ToList();
		stats[mt] = (grp.Count, lens.Min(), lens.Max(), grp.Average(g => Shannon(g.data)),
					 grp.Max(g => MaxZeroRun(g.data)), lens.Distinct().Count());
	}
	static int Tier((int n, int lmin, int lmax, double ent, int zr, int kinds) v) =>
		(v.kinds >= 3 && v.zr <= 8 && v.ent >= 3.0) ? 3 :
		(v.zr <= 8 && v.ent >= 3.0) ? 2 : (v.ent >= 3.0) ? 1 : 0;
	var arrMt = stats.OrderByDescending(kv => (Tier(kv.Value), kv.Value.ent, kv.Value.n)).First().Key;

	var objs = cands.Where(c => c.mt == arrMt).OrderBy(c => c.ofs).ToList();
	return objs.Select(o => (Ofs2Va(secs, img, o.ofs), o.ln, o.data)).ToList();
}

// ---------------- PE 辅助（与种子定位器一致） ----------------
static long Va2Ofs(List<(string name, uint vaddr, uint vsize, uint raddr, uint rsize)> secs, long img, long va)
{
	long rva = va - img;
	foreach (var (_, vaddr, vsize, raddr, rsize) in secs)
		if (vaddr <= rva && rva < vaddr + Math.Max(vsize, rsize))
			return rva - vaddr < rsize ? raddr + (rva - vaddr) : -1;
	return -1;
}

static long Ofs2Va(List<(string name, uint vaddr, uint vsize, uint raddr, uint rsize)> secs, long img, long ofs)
{
	foreach (var (_, vaddr, vsize, raddr, rsize) in secs)
		if (raddr <= ofs && ofs < raddr + rsize) return img + vaddr + (ofs - raddr);
	return -1;
}

static uint U32(byte[] d, int o) => BitConverter.ToUInt32(d, o);
static ulong U64(byte[] d, int o) => BitConverter.ToUInt64(d, o);

static bool LooksUtf16(byte[] d, int off, int len)
{
	if (len == 0 || (len & 1) != 0 || off + len > d.Length) return false;
	var s = Encoding.Unicode.GetString(d, off, len);
	foreach (char c in s) if (c < ' ' || c >= 0xFFFD) return false;
	return s.Length > 0;
}

static double Shannon(byte[] b)
{
	if (b.Length == 0) return 0;
	var cnt = new Dictionary<byte, int>();
	foreach (var x in b) cnt[x] = cnt.GetValueOrDefault(x) + 1;
	double e = 0;
	foreach (var v in cnt.Values) { double p = (double)v / b.Length; e -= p * Math.Log2(p); }
	return e;
}

static int MaxZeroRun(byte[] b)
{
	int run = 0, best = 0;
	foreach (var x in b) { run = x == 0 ? run + 1 : 0; best = Math.Max(best, run); }
	return best;
}

// ============================ PE 解析 ============================
static (long img, List<(string name, uint vaddr, uint vsize, uint raddr, uint rsize)>) ParsePe(byte[] d)
{
	int pe = BitConverter.ToInt32(d, 0x3C);
	if (BitConverter.ToUInt32(d, pe) != 0x00004550) throw new Exception("不是 PE 文件");
	int nsec = BitConverter.ToUInt16(d, pe + 6);
	int sizeopt = BitConverter.ToUInt16(d, pe + 20);
	int opt = pe + 24;
	ushort magic = BitConverter.ToUInt16(d, opt);
	long img = magic == 0x20B ? BitConverter.ToInt64(d, opt + 24) : BitConverter.ToUInt32(d, opt + 28);
	var secs = new List<(string, uint, uint, uint, uint)>();
	int off = opt + sizeopt;
	for (int i = 0; i < nsec; i++)
	{
		string name = Encoding.ASCII.GetString(d, off, 8).TrimEnd('\0');
		uint vs = BitConverter.ToUInt32(d, off + 8), va = BitConverter.ToUInt32(d, off + 12);
		uint rs = BitConverter.ToUInt32(d, off + 16), ra = BitConverter.ToUInt32(d, off + 20);
		secs.Add((name, va, vs, ra, rs)); off += 40;
	}
	return (img, secs);
}

// ============================ x86-64 迷你反汇编器 ============================
class Ins
{
	public string Mn;
	public int Len;
	public int Reg1 = -1, Reg2 = -1;      // 寄存器编号(0-15, 已含 REX 扩展)
	public int RmBase = -1;               // 内存基址寄存器
	public bool RmIsRip;
	public long RmDisp;
	public long Imm;
	public bool ImmSrc;
	public int StoreSize;                 // 内存写入字节数（mov 到内存时）
	public long RelTarget;
}

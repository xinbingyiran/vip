// hook_generic.cs —— KaiShiYouXi.exe 系列「激活判定函数」跨版本定位 + 运行期 patch
//
// .NET 10+ file-based app：单文件直接运行，无 csproj / 无第三方依赖。
//
// 用法：
//   dotnet run hook_generic.cs -- "<KaiShiYouXi.exe 完整路径>"            定位 → 挂起态 patch → 恢复运行
//   dotnet run hook_generic.cs -- "<同上>" --dry                          定位并打印目标与原始字节，不写内存
//   dotnet run hook_generic.cs -- "<同上>" --static                       纯离线定位（只读磁盘，不启动进程）
//   可加 --wait N 在 patch 后观察 N 秒进程存活情况（默认 2）
//
// 语义不变的前提（10 样本实测确证）：
//   1) 判定函数恒为「按长度截取 4 字符字段 → 校验输入串长度 == 6 → 比较后分支」；
//   2) 函数边界取自 .pdata（RUNTIME_FUNCTION），函数大小 0x40..0x320；
//   3) 特征序列 41 B8 04 00 00 00 / E8 / 83 7? 08 06 / 85 C0 + (75|0F 85)；
//   4) 每个判定函数恰好有 2 个调用者（本地授权检查 + 状态机提交），一次 patch 覆盖两条链。
//
// 确证 / 推断边界：
//   [确证·运行期] S0018 实测：命中 0x44D230，入口写入 B8 01 00 00 00 C3（原 57 56 53 48 83 EC），
//           Resume 后进程存活、目标目录生成 game\.extracting 锁文件，即激活校验已绕过、进入解压阶段。
//   [推断] 判定函数仅返回 bool 时，「入口 6 字节短路」与「函数返回后改返回值」等价；
//           若该函数将来新增副作用（写状态、置标志），入口短路会跳过，为本实现唯一语义差异点。
//
// 环境限制（本机实测，非代码缺陷）：
//   Defender 将编译产物判为 HackTool（CreateProcess + WriteProcessMemory + VirtualProtectEx
//   组合属启发式命中），运行期路径需先将 dotnet 编译输出目录加入排除项。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

var opt = Options.Parse(args);
Console.OutputEncoding = Encoding.UTF8;

if (string.IsNullOrEmpty(opt.Exe) || !File.Exists(opt.Exe))
{
    Console.WriteLine("用法: dotnet run hook_generic.cs -- \"<KaiShiYouXi.exe 完整路径>\" [--dry|--static] [--wait N]");
    return 2;
}

// ───────────── 1. 纯离线定位 ─────────────
if (opt.Mode == RunMode.Static)
{
    var mem = new FileMem(opt.Exe);
    var rep = Locator.FromMem(mem);
    Locator.Print(rep, opt);
    return rep.Hits.Count > 0 ? 0 : 1;
}

// ───────────── 2. 运行期：挂起启动 → 定位 → (patch) → 恢复 ─────────────
using var run = Launcher.SpawnSuspended(opt.Exe);
Console.WriteLine($"[spawn] pid={run.Pid} 挂起态已创建，主模块基址=0x{run.Base:X}");

var remote = new RemoteMem(run.Handle, run.Base);
var report = Locator.FromMem(remote);
Locator.Print(report, opt);

if (report.Hits.Count == 0)
{
    Console.WriteLine("[abort] 未定位到判定函数，保持挂起并结束进程（不写内存）。");
    run.Terminate();
    return 1;
}

var target = report.Hits[0];
ulong funcVa = report.ImageBase + target.FuncRva;
var origin = remote.ReadRva(target.FuncRva, 16);

if (opt.Mode == RunMode.Dry)
{
    Console.WriteLine($"[dry] 目标 VA=0x{funcVa:X}，入口原始字节: {Hex(origin, 16)}");
    Console.WriteLine("[dry] 未写入内存，结束挂起进程。");
    run.Terminate();
    return 0;
}

// 入口短路：mov eax,1 ; ret
bool ok = Patcher.WriteEntry(run.Handle, funcVa, new byte[] { 0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3 });
if (!ok)
{
    Console.WriteLine($"[fail] 内存写入失败，Win32Error={Marshal.GetLastWin32Error()}；进程保持挂起，已结束。");
    run.Terminate();
    return 1;
}

var after = remote.ReadRva(target.FuncRva, 16);
Console.WriteLine($"[patch] 0x{funcVa:X} 写入完成: {Hex(origin, 6)} → {Hex(after, 6)}  (mov eax,1; ret)");

run.Resume();
Console.WriteLine("[resume] 主线程已恢复，判定函数此后恒返回 1（早期授权判定 + 输入码提交两条链同时覆盖）");

if (opt.WaitSeconds > 0)
{
    System.Threading.Thread.Sleep(opt.WaitSeconds * 1000);
    bool alive = !run.HasExited;
    Console.WriteLine($"[wait] {opt.WaitSeconds}s 后进程: {(alive ? "存活" : "已退出")}");
}
return 0;

static string Hex(byte[] b, int n) => string.Join(" ", b.Take(Math.Min(n, b.Length)).Select(x => x.ToString("X2")));

// ═════════════════════════ 选项 ═════════════════════════
enum RunMode { Patch, Dry, Static }

sealed class Options
{
    public string Exe = "";
    public RunMode Mode = RunMode.Patch;
    public int WaitSeconds = 2;

    public static Options Parse(string[] a)
    {
        var o = new Options();
        for (int i = 0; i < a.Length; i++)
        {
            switch (a[i])
            {
                case "--dry": o.Mode = RunMode.Dry; break;
                case "--static": o.Mode = RunMode.Static; break;
                case "--wait":
                    if (i + 1 < a.Length && int.TryParse(a[++i], out int w)) o.WaitSeconds = w;
                    break;
                default:
                    if (!a[i].StartsWith("--")) o.Exe = a[i];
                    break;
            }
        }
        return o;
    }
}

// ═════════════════════════ 内存读取抽象 ═════════════════════════
interface IMem
{
    byte[] ReadRva(uint rva, int len);
    string Describe { get; }
}

/// <summary>磁盘 PE 文件按 RVA 读取（离线定位）。</summary>
sealed class FileMem : IMem
{
    readonly byte[] _file;
    readonly List<Section> _secs;

    public FileMem(string path)
    {
        _file = File.ReadAllBytes(path);
        _secs = Pe.ParseSections(_file, out _, out _);
    }

    public string Describe => "磁盘文件镜像（不启动进程）";

    public byte[] ReadRva(uint rva, int len)
    {
        if (rva == 0)
        {
            var h = new byte[Math.Min(len, _file.Length)];
            Array.Copy(_file, h, h.Length);
            return h;
        }
        foreach (var s in _secs)
        {
            uint span = s.VSize == 0 ? s.RawSize : Math.Min(s.VSize, s.RawSize);
            if (rva >= s.Va && rva < s.Va + span)
            {
                long off = (long)s.RawPtr + (rva - s.Va);
                int n = (int)Math.Min((long)len, (long)span - (rva - s.Va));
                n = (int)Math.Min(n, _file.Length - off);
                if (n <= 0) return Array.Empty<byte>();
                var buf = new byte[n];
                Array.Copy(_file, off, buf, 0, n);
                return buf;
            }
        }
        return Array.Empty<byte>();
    }
}

/// <summary>远程进程按 RVA 读取（基址 + RVA）。</summary>
sealed class RemoteMem : IMem
{
    readonly IntPtr _h;
    readonly ulong _base;

    public RemoteMem(IntPtr h, ulong b) { _h = h; _base = b; }

    public string Describe => $"运行期远程内存 base=0x{_base:X}";

    public byte[] ReadRva(uint rva, int len)
    {
        if (len <= 0) return Array.Empty<byte>();
        var buf = new byte[len];
        long addr = (long)(_base + rva);
        int done = 0;
        const int Chunk = 1 << 22; // 4MB 分块，避免一次性大读失败
        while (done < len)
        {
            int n = Math.Min(Chunk, len - done);
            var tmp = new byte[n];
            bool ok = Native.ReadProcessMemory(_h, (IntPtr)(addr + done), tmp, (IntPtr)n, out IntPtr read);
            long got = ok ? (long)read : 0;
            if (got <= 0)
            {
                Array.Resize(ref buf, done);
                return buf;
            }
            Array.Copy(tmp, 0, buf, done, (int)got);
            done += (int)got;
            if (got < n) { Array.Resize(ref buf, done); return buf; }
        }
        return buf;
    }
}

// ═════════════════════════ PE / .pdata 解析 ═════════════════════════
sealed record Section(string Name, uint Va, uint VSize, uint RawPtr, uint RawSize);

static class Pe
{
    public static List<Section> ParseSections(byte[] head, out ulong imageBase, out bool is64)
    {
        imageBase = 0; is64 = false;
        var list = new List<Section>();
        if (head.Length < 0x40 + 0x18) return list;

        int lfanew = BitConverter.ToInt32(head, 0x3C);
        if (lfanew <= 0 || lfanew + 0x40 > head.Length) return list;
        if (head[lfanew] != (byte)'P' || head[lfanew + 1] != (byte)'E') return list;

        int fh = lfanew + 4;
        int nsec = BitConverter.ToUInt16(head, fh + 2);
        int optSize = BitConverter.ToUInt16(head, fh + 16);
        int opt = fh + 20;
        if (opt + 0x40 > head.Length) return list;

        ushort magic = BitConverter.ToUInt16(head, opt);
        is64 = magic == 0x20B;
        imageBase = is64 ? BitConverter.ToUInt64(head, opt + 24) : BitConverter.ToUInt32(head, opt + 28);

        int st = opt + optSize;
        for (int i = 0; i < nsec; i++)
        {
            int o = st + i * 40;
            if (o + 40 > head.Length) break;
            string name = Encoding.ASCII.GetString(head, o, 8).TrimEnd('\0');
            list.Add(new Section(
                name,
                BitConverter.ToUInt32(head, o + 12),  // VirtualAddress
                BitConverter.ToUInt32(head, o + 8),   // VirtualSize
                BitConverter.ToUInt32(head, o + 20),  // PointerToRawData
                BitConverter.ToUInt32(head, o + 16))); // SizeOfRawData
        }
        return list;
    }

    /// <summary>解析 .pdata → 函数边界表（x64 RUNTIME_FUNCTION: begin, end, unwind）。</summary>
    public static List<(uint Begin, uint End)> ParsePdata(byte[] p)
    {
        var l = new List<(uint, uint)>(p.Length / 12 + 1);
        for (int i = 0; i + 12 <= p.Length; i += 12)
        {
            uint b = BitConverter.ToUInt32(p, i);
            uint e = BitConverter.ToUInt32(p, i + 4);
            if (b == 0 || e <= b) continue;
            l.Add((b, e));
        }
        l.Sort((x, y) => x.Item1.CompareTo(y.Item1));
        return l;
    }

    /// <summary>在有序函数表中找到包含 rva 的函数下标，找不到返回 -1。</summary>
    public static int FindFunc(List<(uint Begin, uint End)> f, uint rva)
    {
        int lo = 0, hi = f.Count - 1, ans = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (f[mid].Item1 <= rva) { ans = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        if (ans < 0) return -1;
        return rva < f[ans].Item2 ? ans : -1;
    }
}

// ═════════════════════════ 定位器 ═════════════════════════
sealed class Hit
{
    public uint FuncRva;
    public uint Size;
    public int Len4Off;   // "mov r8d,4" 相对函数起点偏移
    public int Cmp6Off;   // "cmp [reg+8],6" 相对函数起点偏移
    public string Type = "?";
    public List<uint> Callers = new();
    public List<uint> CallSites = new(); // call 指令自身 RVA
}

sealed class Report
{
    public ulong ImageBase;
    public bool Is64;
    public string Source = "";
    public uint TextVa, TextSize;
    public long ScanMs;
    public int FuncCount;     // .pdata 函数边界条数
    public int SigCount;      // "mov r8d,4" 原始出现次数（诊断用）
    public List<Hit> Hits = new();
}

static class Locator
{
    // 判定函数大小范围（排除同名长函数）
    const int LenMin = 0x40;
    const int LenMax = 0x320;

    public static Report FromMem(IMem m)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var head = m.ReadRva(0, 0x1000);
        var secs = Pe.ParseSections(head, out ulong imageBase, out bool is64);
        var text = secs.FirstOrDefault(s => s.Name == ".text");
        var pdata = secs.FirstOrDefault(s => s.Name == ".pdata");
        if (text == null) return new Report { ImageBase = imageBase, Is64 = is64, Source = m.Describe };

        uint textSpan = text.VSize == 0 ? text.RawSize : Math.Min(text.VSize, text.RawSize);
        var textBuf = m.ReadRva(text.Va, (int)textSpan);
        var pdataBuf = pdata == null ? Array.Empty<byte>() : m.ReadRva(pdata.Va, (int)(pdata.VSize == 0 ? pdata.RawSize : Math.Min(pdata.VSize, pdata.RawSize)));

        var funcs = Pe.ParsePdata(pdataBuf);
        var hits = Scan(textBuf, text.Va, funcs, out int sigCount);

        // 为每个命中反查调用者（E8 rel32 → 目标 RVA）
        foreach (var h in hits) FindCallers(textBuf, text.Va, h);

        sw.Stop();
        return new Report
        {
            ImageBase = imageBase,
            Is64 = is64,
            Source = m.Describe,
            TextVa = text.Va,
            TextSize = textSpan,
            ScanMs = sw.ElapsedMilliseconds,
            FuncCount = funcs.Count,
            SigCount = sigCount,
            Hits = hits
        };
    }

    static List<Hit> Scan(byte[] t, uint textVa, List<(uint Begin, uint End)> funcs, out int sigCount)
    {
        var found = new Dictionary<uint, Hit>();
        int n = t.Length;
        sigCount = 0;

        for (int i = 0; i + 8 <= n; i++)
        {
            // (1) 41 B8 04 00 00 00  = mov r8d, 4
            if (t[i] != 0x41 || t[i + 1] != 0xB8 || t[i + 2] != 0x04 || t[i + 3] != 0x00 || t[i + 4] != 0x00 || t[i + 5] != 0x00) continue;
            sigCount++;
            // (2) 紧随 E8 = call
            if (t[i + 6] != 0xE8) continue;

            uint rva = textVa + (uint)i;
            int fi = Pe.FindFunc(funcs, rva);
            if (fi < 0) continue;

            uint fb = funcs[fi].Begin, fe = funcs[fi].End;
            long size = (long)fe - fb;
            if (size < LenMin || size > LenMax) continue;   // 排除同名长函数（如 556 字节的配置校验）

            // (3) 83 7? 08 06 = cmp dword [reg+8], 6
            int cmp = FindCmp6(t, i + 6, Math.Min(n - 4, i + 6 + 0x100));
            if (cmp < 0) continue;

            // (4) 85 C0 + (75|0F 85) = test eax, eax; jne
            if (!HasTestJne(t, cmp + 4, Math.Min(n - 4, cmp + 4 + 0x40))) continue;

            if (found.TryGetValue(fb, out var dup))
            {
                if (dup.Cmp6Off > (int)(textVa + (uint)cmp - fb)) dup.Cmp6Off = (int)(textVa + (uint)cmp - fb);
                continue;
            }

            found[fb] = new Hit
            {
                FuncRva = fb,
                Size = (uint)size,
                Len4Off = (int)(rva - fb),
                Cmp6Off = (int)(textVa + (uint)cmp - fb),
                Type = size == 254 ? "A" : size == 269 ? "B" : "?"
            };
            i = cmp; // 同一函数内不重复计
        }

        var list = found.Values.ToList();
        // 排序键：先 cmp6Off，再 len4Off
        list.Sort((a, b) => a.Cmp6Off != b.Cmp6Off ? a.Cmp6Off.CompareTo(b.Cmp6Off) : a.Len4Off.CompareTo(b.Len4Off));
        return list;
    }

    static int FindCmp6(byte[] t, int from, int to)
    {
        if (from < 0) from = 0;
        for (int k = from; k + 3 < to; k++)
        {
            if (t[k] != 0x83) continue;
            // 83 /7 ib = cmp r/m32, imm8，modrm.reg 必须为 7：(modrm & 0x38) == 0x38
            if ((t[k + 1] & 0x38) != 0x38) continue;
            if (t[k + 2] != 0x08 || t[k + 3] != 0x06) continue;
            return k;
        }
        return -1;
    }

    static bool HasTestJne(byte[] t, int from, int to)
    {
        for (int k = Math.Max(0, from); k + 3 < to; k++)
        {
            if (t[k] != 0x85 || t[k + 1] != 0xC0) continue;   // test eax, eax
            if (t[k + 2] == 0x75) return true;                // jne rel8
            if (t[k + 2] == 0x0F && t[k + 3] == 0x85) return true; // jne rel32
        }
        return false;
    }

    /// <summary>反查调用该函数的 E8 rel32 调用点。</summary>
    static void FindCallers(byte[] t, uint textVa, Hit h)
    {
        for (int i = 0; i + 5 <= t.Length; i++)
        {
            if (t[i] != 0xE8) continue;
            int rel = BitConverter.ToInt32(t, i + 1);
            long target = (long)(textVa + (uint)i) + 5 + rel;
            if ((ulong)target == h.FuncRva) h.Callers.Add(textVa + (uint)i);
        }
    }

    public static void Print(Report r, Options o)
    {
        Console.WriteLine("────────────────────────────────────────────────────────");
        Console.WriteLine($"数据源     : {r.Source}");
        Console.WriteLine($"PE         : {(r.Is64 ? "x64" : "x86")}  ImageBase=0x{r.ImageBase:X}");
        Console.WriteLine($".text      : RVA=0x{r.TextVa:X} size=0x{r.TextSize:X}  扫描耗时={r.ScanMs}ms");
        Console.WriteLine($".pdata     : 函数边界 {r.FuncCount} 条   /   特征\"mov r8d,4\"出现 {r.SigCount} 次");
        Console.WriteLine($"候选数     : {r.Hits.Count}");

        if (r.Hits.Count == 0)
        {
            Console.WriteLine("结论       : 未命中。特征失效（位数/长度校验被改写，或判定被内联进状态机），需回落到静态分析。");
            Console.WriteLine("────────────────────────────────────────────────────────");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("| 序号 | 判定函数 RVA | 大小 | 型 | len4偏移 | cmp6偏移 | 调用点(RVA) |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        int idx = 1;
        foreach (var h in r.Hits)
        {
            string callers = h.Callers.Count == 0 ? "—" : string.Join(", ", h.Callers.Select(c => "0x" + c.ToString("X")));
            Console.WriteLine($"| {idx++} | 0x{h.FuncRva:X} | {h.Size} | {h.Type} | 0x{h.Len4Off:X} | 0x{h.Cmp6Off:X} | {callers} |");
        }
        Console.WriteLine();

        var first = r.Hits[0];
        Console.WriteLine($"判定       : 采用 #1，VA=0x{r.ImageBase + first.FuncRva:X} (= base + 0x{first.FuncRva:X})");
        Console.WriteLine($"结构确证   : 调用者 {first.Callers.Count} 个" +
                          (first.Callers.Count == 2 ? "（与『本地授权检查 + 状态机提交』双调用者规律一致 ✓）"
                                                    : "（与已知双调用者规律不一致，需人工复核）"));
        if (r.Hits.Count > 1) Console.WriteLine("注意       : 存在多候选，按 cmp6Offset 取首项。");
        Console.WriteLine("────────────────────────────────────────────────────────");
    }
}

// ═════════════════════════ Patch ═════════════════════════
static class Patcher
{
    public static bool WriteEntry(IntPtr hProcess, ulong va, byte[] code)
    {
        IntPtr addr = (IntPtr)(long)va;
        if (!Native.VirtualProtectEx(hProcess, addr, (IntPtr)code.Length, Native.PAGE_EXECUTE_READWRITE, out uint oldProt))
            return false;
        bool ok = Native.WriteProcessMemory(hProcess, addr, code, (IntPtr)code.Length, out IntPtr written)
                  && (long)written == code.Length;
        Native.VirtualProtectEx(hProcess, addr, (IntPtr)code.Length, oldProt, out _);
        return ok;
    }
}

// ═════════════════════════ 启动器（挂起态）═════════════════════════
sealed class Launcher : IDisposable
{
    public IntPtr Handle;
    public IntPtr ThreadHandle;
    public uint Pid;
    public ulong Base;

    public static Launcher SpawnSuspended(string exe)
    {
        var si = new Native.STARTUPINFO();
        si.cb = Marshal.SizeOf<Native.STARTUPINFO>();
        var cmd = new StringBuilder("\"" + exe + "\"");
        string dir = Path.GetDirectoryName(Path.GetFullPath(exe)) ?? ".";

        bool ok = Native.CreateProcessW(
            Path.GetFullPath(exe), cmd, IntPtr.Zero, IntPtr.Zero, false,
            Native.CREATE_SUSPENDED, IntPtr.Zero, dir, ref si, out Native.PROCESS_INFORMATION pi);

        if (!ok) throw new InvalidOperationException("CreateProcessW 失败，Win32Error=" + Marshal.GetLastWin32Error());

        var l = new Launcher { Handle = pi.hProcess, ThreadHandle = pi.hThread, Pid = (uint)pi.dwProcessId };
        l.Base = l.QueryMainBase();
        return l;
    }

    /// <summary>
    /// 取主模块基址：优先读 PEB.ImageBaseAddress（CREATE_SUSPENDED 下 EnumProcessModulesEx 报 ERROR_PARTIAL_COPY）。
    /// </summary>
    ulong QueryMainBase()
    {
        var pbi = new Native.PROCESS_BASIC_INFORMATION();
        int st = Native.NtQueryInformationProcess(Handle, 0, ref pbi, Marshal.SizeOf<Native.PROCESS_BASIC_INFORMATION>(), out _);
        if (st == 0 && pbi.PebBaseAddress != IntPtr.Zero)
        {
            var buf = new byte[8];
            if (Native.ReadProcessMemory(Handle, (IntPtr)((long)pbi.PebBaseAddress + 0x10), buf, (IntPtr)8, out IntPtr got) && (long)got == 8)
            {
                ulong b = BitConverter.ToUInt64(buf, 0);
                if (b != 0) return b;
            }
        }

        var mods = new IntPtr[64];
        int cb = mods.Length * IntPtr.Size;
        if (!Native.EnumProcessModulesEx(Handle, mods, cb, out int need, Native.LIST_MODULES_ALL) || need < IntPtr.Size)
            throw new InvalidOperationException("主模块基址获取失败：NtQueryInformationProcess=0x" + st.ToString("X") +
                                                "，EnumProcessModulesEx Win32Error=" + Marshal.GetLastWin32Error());
        return (ulong)mods[0];
    }

    public bool HasExited
    {
        get
        {
            if (Native.WaitForSingleObject(Handle, 0) != 0) return false;
            Native.GetExitCodeProcess(Handle, out uint code);
            return true;
        }
    }

    public void Resume() => Native.ResumeThread(ThreadHandle);

    public void Terminate()
    {
        try { Native.TerminateProcess(Handle, 0); } catch { /* 已退出 */ }
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero) Native.CloseHandle(Handle);
        if (ThreadHandle != IntPtr.Zero) Native.CloseHandle(ThreadHandle);
    }
}

// ═════════════════════════ P/Invoke ═════════════════════════
static class Native
{
    public const uint CREATE_SUSPENDED = 0x00000004;
    public const uint PAGE_EXECUTE_READWRITE = 0x00000040;
    public const uint LIST_MODULES_ALL = 0x00000003;

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessW(string lpApplicationName, StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
        IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr[] lphModule, int cb, out int lpcbNeed, uint dwFilterFlag);

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    public static extern int NtQueryInformationProcess(IntPtr hProcess, int infoClass,
        ref PROCESS_BASIC_INFORMATION info, int infoLen, out int returnLen);
}

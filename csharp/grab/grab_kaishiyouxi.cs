#:package System.Reflection.MetadataLoadContext@6.0.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

// 本脚本是静态元数据分析工具(仅离线读元数据,无发布/裁剪场景),
// 以下 IL20xx 裁剪分析器警告均为误报,统一抑制。
#pragma warning disable IL2026, IL2075

// =====================================================================
// 用法:
//   dotnet run scan_gamedata.cs -- <文件或目录...>
// 可混合传入 .dll 文件或目录;目录会扫描其中所有 *.dll(浅层)。
// 例:
//   dotnet run scan_gamedata.cs -- D:\X\Downloads
//   dotnet run scan_gamedata.cs -- D:\X\Downloads\W0198\...\KaiShiYouXi.dll D:\X\Downloads\C0042
// =====================================================================

if (args.Length == 0)
{
    Console.Error.WriteLine("用法: dotnet run scan_gamedata.cs -- <dll文件或目录...>");
    return;
}
var targets = args.Select(Path.GetFullPath).ToArray();

// 单文件 app 下 Assembly.Location 返回空串,改用运行时目录
var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();

foreach (var raw in targets)
{
    // ---- 目标判定:文件还是目录 ----
    string[] files;
    if (Directory.Exists(raw))
    {
        // 目录递归扫描 KaiShiYouXi.dll(忽略大小写)
        files = Directory.GetFiles(raw, "KaiShiYouXi.dll", SearchOption.AllDirectories)
                         .ToArray();
        if (files.Length == 0)
        {
            Console.Error.WriteLine($"[SKIP] 目录 {raw} 下没有 KaiShiYouXi.dll");
            continue;
        }
    }
    else if (File.Exists(raw))
    {
        if (!raw.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"[SKIP] 非 dll 文件(仅分析托管程序集): {raw}");
            continue;
        }
        files = new[] { raw };
    }
    else
    {
        Console.Error.WriteLine($"[SKIP] 路径不存在: {raw}");
        continue;
    }

    foreach (var path in files)
    {
        ScanOne(path, runtimeDir);
    }
}

return;

// =====================================================================
static void ScanOne(string path, string runtimeDir)
{
    try
    {
        var fi = new FileInfo(path);
        if (!fi.Exists || fi.Length == 0)
        {
            Console.Error.WriteLine($"[SKIP] 文件不存在或为空: {path}");
            return;
        }
        // 粗略判定:读 PE 头,确认是 PE 且带 .NET 目录,避免把原生 dll 送进 MetadataLoadContext
        bool isManaged = PeHasManagedHeader(path);
        if (!isManaged)
        {
            Console.Error.WriteLine($"[SKIP] 非托管 PE(无 CLR 头): {path}");
            return;
        }

        var paths = new List<string>();
        // System*.dll 已覆盖 System.Private.CoreLib / System.Private.Uri 等运行时程序集,
        // 无需再引用 Assembly.Location(单文件 app 下为空串)
        paths.AddRange(Directory.GetFiles(runtimeDir, "System*.dll"));
        paths.Add(Path.Combine(runtimeDir, "netstandard.dll"));
        var dir = Path.GetDirectoryName(path);
        if (dir != null) paths.Add(dir);
        var resolver = new PathAssemblyResolver(paths.Distinct());

        using var mlc = new MetadataLoadContext(resolver);
        Assembly asm;
        try
        {
            asm = mlc.LoadFromAssemblyPath(path);
        }
        catch (BadImageFormatException)
        {
            Console.Error.WriteLine($"[SKIP] 不是有效托管程序集: {path}");
            return;
        }

        Type[] types;
        try
        {
            types = asm.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // 部分类型无法加载(常见于被保护/弱引用类型),可用的仍可枚举
            types = ex.Types.Where(t => t != null).OrderBy(t => t!.FullName, StringComparer.Ordinal).Cast<Type>().ToArray();
        }
        Console.WriteLine(path);

        var gd = types.Where(t => t.Name == "GameConfigData").ToArray();
        foreach (var t in gd)
        {
            FieldInfo[] flds;
            try
            {
                flds = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(f => f.IsLiteral).OrderBy(f => f.MetadataToken).ToArray();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"      读取 {t.FullName} 字段失败: {ex.GetType().Name}: {ex.Message}");
                continue;
            }
            foreach (var f in flds)
            {
                string? raw;
                try
                {
                    var v = f.GetRawConstantValue();
                    raw = v switch
                    {
                        null => null,
                        string s => s,
                        _ => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)
                    };
                }
                catch { continue; } // err 字段不输出
                if (string.IsNullOrWhiteSpace(raw)) continue; // 空/空白字段不输出
                Console.WriteLine($"      {f.Name} = \"{raw}\"");
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[FAIL] {path}: {ex.GetType().Name}: {ex.Message}");
    }
}

// 解析 PE 头:判断是否存在 CLR 数据目录(COM descriptor),从而判定是否 .NET 程序集
static bool PeHasManagedHeader(string path)
{
    try
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);
        if (fs.Length < 0x40) return false;
        fs.Position = 0x3C;                     // e_lfanew
        int peOff = br.ReadInt32();
        if (peOff <= 0 || peOff + 0x18 > fs.Length) return false;
        fs.Position = peOff;
        if (br.ReadUInt32() != 0x00004550) return false; // "PE\0\0"
        fs.Position = peOff + 4;
        ushort machine = br.ReadUInt16();
        if (machine != 0x014c && machine != 0x8664 && machine != 0xaa64 && machine != 0x01c4) return false;
        fs.Position = peOff + 0x18;             // optional header
        ushort magic = br.ReadUInt16();         // 0x10b PE32 / 0x20b PE32+
        if (magic != 0x10b && magic != 0x20b) return false;
        int dirOff = magic == 0x10b ? peOff + 0x18 + 0x60 : peOff + 0x18 + 0x70; // data directory 起点
        // CLR header 是 data directory 的第 15 项(索引 14)
        fs.Position = dirOff + 14 * 8;
        return br.ReadUInt32() != 0;            // COM descriptor RVA 非 0 => 托管程序集
    }
    catch
    {
        return false;
    }
}

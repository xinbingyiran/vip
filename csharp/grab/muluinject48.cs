// muluInject.cs: 启动目标exe, 用 ClrMD 定位 JiHuo 实例地址, 注入后直接调用 JieYaGuoCheng
// 用法: dotnet run muluInject.cs <注入目标exe完整路径>
#:include FastWin32.cs
#:property TargetFramework=net48
#:property UseWPF=true
#:property LangVersion=latest
#:property PublishAot=false
#:property AllowUnsafeBlocks=true
#:package Microsoft.NETFramework.ReferenceAssemblies@1.0.3
#:package Microsoft.Diagnostics.Runtime@3.1.512801

using FastWin32.Diagnostics;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

if (args.Length == 1 && args[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
{
    var file = Path.GetFullPath(args[0]);
    var dllFile = Assembly.GetEntryAssembly().Location;

    var p = Process.Start(file);
    while (true)
    {
        p.Refresh();
        if (p.HasExited) return;
        if (p.MainWindowHandle != IntPtr.Zero) break;
        Thread.Sleep(100);
    }

    Thread.Sleep(1000);

    using var dt = DataTarget.AttachToProcess(p.Id, suspend: true);
    using var rt = dt.ClrVersions[0].CreateRuntime();
    var addr = rt.Heap.EnumerateObjects().FirstOrDefault(o => o.Type?.Name == "JiHuoA.JiHuo").Address;

    Injector.InjectManaged((uint)p.Id, dllFile, "Trigger", "Inject", addr.ToString(CultureInfo.InvariantCulture));
    Console.WriteLine("injected: " + dllFile);
}
else
{
    Console.WriteLine("用法: " + Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName) + " <注入目标exe路径>");
}

// 注入入口: 目标进程加载本程序集后, 用传入的实例地址直接调用 JieYaGuoCheng
public static class Trigger
{
    public static int Inject(string args)
    {
        try
        {
            var app = Application.Current;
            if (app == null) return -6; // 无 WPF Application
            if (!ulong.TryParse(args, out ulong addr)) return -5; // 无有效地址
            var raw = new IntPtr(unchecked((long)addr));
            var inst = Unsafe.As<IntPtr, object>(ref raw);
            if (inst == null) return -4;
            var m = inst.GetType().GetMethod("JieYaGuoCheng", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null) return -3;
            app.Dispatcher.Invoke(() =>
            {
                StaticFieldDumper.Dump(Environment.GetCommandLineArgs()[0]);
                m.Invoke(inst, null);
            });
            return 1;
        }
        catch
        {
            return -1;
        }
    }
}

// 注入成功后：枚举目标程序集内定义的类型的静态字段/常量，并从 Application.Current
// 向下枚举实例对象图，JSON 序列化保存到目标 exe 目录
public static class StaticFieldDumper
{
    private const int MaxDepth = 12;
    private const int MaxItems = 20000;

    private sealed class RefEqComparer : IEqualityComparer<object>
    {
        public static readonly RefEqComparer Instance = new RefEqComparer();
        public new bool Equals(object x, object y) { return ReferenceEquals(x, y); }
        public int GetHashCode(object obj) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj); }
    }

    private sealed class DumpContext
    {
        public readonly HashSet<object> Visited = new HashSet<object>(RefEqComparer.Instance);
        public Assembly? TargetAsm;
    }

    // 轻量 JSON 写入器：对象/数组 + 属性/元素逗号管理 + 层级缩进（每层2空格），字符串转义，中文原样保留
    private sealed class JsonBuf
    {
        private readonly StringBuilder sb;
        private bool needComma;
        private int depth;
        private readonly Stack<bool> contentStack = new Stack<bool>(); // 记录各层容器是否已写入内容（空容器不换行，输出 "{}"/"[]"）
        private bool hasContent;

        public JsonBuf(StringBuilder sb) { this.sb = sb; }

        private void BeforeMember()
        {
            if (needComma) sb.Append(',');
            sb.Append('\n').Append(' ', depth * 2);
        }

        public void StartObject()
        {
            contentStack.Push(hasContent);
            hasContent = false;
            sb.Append('{');
            depth++;
            needComma = false;
        }
        public void EndObject()
        {
            depth--;
            if (hasContent) sb.Append('\n').Append(' ', depth * 2);
            sb.Append('}');
            hasContent = contentStack.Pop();
            needComma = true;
        }
        public void StartArray()
        {
            contentStack.Push(hasContent);
            hasContent = false;
            sb.Append('[');
            depth++;
            needComma = false;
        }
        public void EndArray()
        {
            depth--;
            if (hasContent) sb.Append('\n').Append(' ', depth * 2);
            sb.Append(']');
            hasContent = contentStack.Pop();
            needComma = true;
        }
        public void Property(string name)
        {
            BeforeMember();
            AppendJsonString(sb, name);
            sb.Append(':');
            needComma = true;
            hasContent = true;
        }
        public void Element()
        {
            BeforeMember();
            needComma = true;
            hasContent = true;
        }
        public void Raw(string s) { sb.Append(s); }
    }

    public static void Dump(string? targetExe)
    {
        try
        {
            // 参数兜底：GetCommandLineArgs 可能为空/非 exe 路径
            if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe))
            {
                try { targetExe = Process.GetCurrentProcess().MainModule?.FileName; } catch { targetExe = null; }
            }
            var dir = Path.GetDirectoryName(targetExe);
            if (string.IsNullOrEmpty(dir)) dir = Environment.CurrentDirectory;

            var ctx = new DumpContext { TargetAsm = ResolveTargetAssembly(targetExe ?? "") };

            // 顶层 JSON 对象：Application.Current / 程序集静态常量 两段
            var sb = new StringBuilder();
            var j = new JsonBuf(sb);
            j.StartObject();

            if (ctx.TargetAsm == null)
            {
                j.Property("error");
                j.Raw(AppendJsonString(new StringBuilder(), "目标程序集未定位: " + targetExe).ToString());
            }
            else
            {
                Application? app;
                try { app = Application.Current; } catch { app = null; }
                Type? appType = null;
                try { appType = app?.GetType(); } catch { }

                // ---- 1. Application.Current：静态/常量优先，再实例图 ----
                j.Property("Application.Current");
                if (app != null)
                {
                    j.StartObject();
                    // 1a. Application.Current 具体类型（App 子类）的静态/常量，优先展示
                    DumpTypeStaticsJson(j, ctx, "Application.Current", appType, false);
                    // 1b. Application.Current 实例图成员
                    WriteMembers(j, ctx, "Application.Current", app, 0);
                    j.EndObject();
                }
                else j.Raw("null");

                // ---- 2. 程序集静态/常量：Application 不可达的值（按类型路径输出）----
                j.Property("程序集静态/常量（Application 不可达）");
                j.StartObject();
                Type[] types;
                try { types = ctx.TargetAsm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                catch { types = new Type[0]; }
                foreach (var type in types)
                {
                    if (type.Assembly != ctx.TargetAsm) continue;
                    if (appType != null && type == appType) continue; // App 子类静态/常量已在 Application.Current 段优先展示
                    DumpTypeStaticsJson(j, ctx, type.FullName, type, true);
                }
                j.EndObject();
            }

            j.EndObject();

            // 输出到目标 exe 目录，JSON 格式化文本
            var file = Path.Combine(dir, Path.GetFileNameWithoutExtension(targetExe) + "_static_fields.json");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine("saved: " + file);
        }
        catch (Exception ex)
        {
            Console.WriteLine("dump failed: " + ex.Message);
            try
            {
                var dir = string.IsNullOrEmpty(targetExe) ? Environment.CurrentDirectory : Path.GetDirectoryName(targetExe);
                if (string.IsNullOrEmpty(dir)) dir = Environment.CurrentDirectory;
                File.WriteAllText(Path.Combine(dir, "dump_error.log"), ex.ToString());
            }
            catch { }
        }
    }

    // 枚举类型静态/常量字段，写入当前已打开的 JSON 对象上下文（复用全局 Visited）
    // fullPathKey=false：key 用字段名（Application.Current 段）；true：key 用 类型.字段（第二段）
    private static void DumpTypeStaticsJson(JsonBuf j, DumpContext ctx, string ownerPath, Type? type, bool fullPathKey)
    {
        if (type == null) return;
        FieldInfo[] fields;
        try { fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy); }
        catch { return; }
        foreach (var f in fields)
        {
            if (f.Name.Contains("k__BackingField")) continue; // 编译器自动生成后备字段，避免与属性名重复
            object value;
            try { value = f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null); } catch { continue; }
            if (!IsValidValue(ctx, value, 0)) continue;
            j.Property(fullPathKey ? (type.FullName + "." + f.Name) : f.Name);
            WriteValue(j, ctx, ownerPath + "." + f.Name, value, 0);
        }
    }

    private static bool IsValidValue(DumpContext ctx, object value, int depth)
    {
        if (value is null || depth >= MaxDepth) { return false; }
        var vt = value.GetType();
        if (value is System.Collections.IEnumerable) return true;
        if (vt.Assembly != ctx.TargetAsm) { return false; }
        if (!ctx.Visited.Add(value)) { return false; }
        return true;
    }

    // 递归写入一个值：基本类型→标量；集合/字典/可枚举→数组/对象；目标程序集对象→成员对象
    private static void WriteValue(JsonBuf j, DumpContext ctx, string path, object value, int depth)
    {
        if (value == null) { j.Raw("null"); return; } // null 保留为 JSON null

        var vt = value.GetType();
        if (IsBasic(vt)) { j.Raw(BasicJson(value)); return; }
        // 集合/字典/可枚举：展开为 JSON 数组/对象
        if (TryWriteCollection(j, ctx, path, value, depth)) return;
        // 非基本、非集合：仅当定义在目标程序集内、未越深度、无循环引用时才展开
        if (vt.Assembly != ctx.TargetAsm || depth >= MaxDepth) { j.Raw("\"\""); return; }

        j.StartObject();
        WriteMembers(j, ctx, path, value, depth + 1);
        j.EndObject();
    }

    // 将对象的实例字段/属性作为 JSON 成员写入当前已打开的对象上下文（静态/常量已通过类型枚举）
    private static void WriteMembers(JsonBuf j, DumpContext ctx, string path, object value, int depth)
    {
        var vt = value.GetType();
        // 实例字段
        FieldInfo[] ifields;
        try { ifields = vt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
        catch { ifields = new FieldInfo[0]; }
        foreach (var f in ifields)
        {
            // 跳过编译器为自动属性生成的 backing field，其值已通过属性输出，避免重复
            if (f.Name.Contains("k__BackingField")) continue;
            object v;
            try { v = f.GetValue(value); } catch { continue; }
            if (!IsValidValue(ctx, v, depth)) continue;
            j.Property(f.Name);
            WriteValue(j, ctx, path + "." + f.Name, v, depth);
        }
        // 实例属性
        PropertyInfo[] props;
        try { props = vt.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
        catch { props = new PropertyInfo[0]; }
        foreach (var p in props)
        {
            if (p.GetIndexParameters().Length > 0) continue;
            var gm = p.GetGetMethod(true);
            if (gm == null) continue;
            object v;
            try { v = gm.Invoke(value, null); } catch { continue; }
            if (!IsValidValue(ctx, v, depth)) continue;
            j.Property(p.Name);
            WriteValue(j, ctx, path + "." + p.Name, v, depth);
        }
    }

    // 集合/字典/可枚举类型：数组→JSON 数组，字典→JSON 对象（键字符串化），可枚举→JSON 数组
    private static bool TryWriteCollection(JsonBuf j, DumpContext ctx, string path, object value, int depth)
    {
        var vt = value.GetType();
        if (vt.IsArray)
        {
            var arr = (Array)value;
            int n = Math.Min(arr.Length, MaxItems);
            j.StartArray();
            for (int i = 0; i < n; i++)
            {
                object e;
                try { e = arr.GetValue(i); } catch { continue; }
                if (!IsValidValue(ctx, e, depth)) continue;
                j.Element();
                WriteValue(j, ctx, path + "[" + i + "]", e, depth);
            }
            j.EndArray();
            return true;
        }
        if (value is System.Collections.IList list)
        {
            int n = Math.Min(list.Count, MaxItems);
            j.StartArray();
            for (int i = 0; i < n; i++)
            {
                object e;
                try { e = list[i]; } catch { continue; }
                if (!IsValidValue(ctx, e, depth)) continue;
                j.Element();
                WriteValue(j, ctx, path + "[" + i + "]", e, depth);
            }
            j.EndArray();
            return true;
        }
        if (value is System.Collections.IDictionary dict)
        {
            int n = 0;
            j.StartObject();
            foreach (System.Collections.DictionaryEntry e in dict)
            {
                if (n++ >= MaxItems) break;
                if (!IsValidValue(ctx, e.Value, depth)) continue;
                var key = e.Key == null ? "?" : e.Key.ToString();
                j.Property(key);
                WriteValue(j, ctx, path + "[\"" + key + "\"]", e.Value, depth);
            }
            j.EndObject();
            return true;
        }
        if (value is System.Collections.IEnumerable en)
        {
            int n = 0;
            j.StartArray();
            foreach (var e in en)
            {
                if (n >= MaxItems) break;
                if (!IsValidValue(ctx, e, depth)) continue;
                j.Element();
                WriteValue(j, ctx, path + "[" + n + "]", e, depth);
                n++;
            }
            j.EndArray();
            return true;
        }
        return false;
    }

    // 基本类型转 JSON 标量：字符串/字符/布尔/数值/日期/枚举/Guid；NaN/Infinity 写字符串
    private static string BasicJson(object value)
    {
        if (value is string s) { return AppendJsonString(new StringBuilder(), s).ToString(); }
        if (value is char c) { return AppendJsonString(new StringBuilder(), c.ToString()).ToString(); }
        if (value is bool b) { return b ? "true" : "false"; }
        if (value is DateTime dt) { return AppendJsonString(new StringBuilder(), dt.ToString("yyyy-MM-dd HH:mm:ss.fff")).ToString(); }
        if (value is DateTimeOffset dto) { return AppendJsonString(new StringBuilder(), dto.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")).ToString(); }
        if (value is TimeSpan ts) { return AppendJsonString(new StringBuilder(), ts.ToString()).ToString(); }
        if (value is Guid g) { return AppendJsonString(new StringBuilder(), g.ToString()).ToString(); }
        if (value is Enum) { return AppendJsonString(new StringBuilder(), value.ToString()).ToString(); }
        if (value is decimal dc) { return dc.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        if (value is double dd)
        {
            if (double.IsNaN(dd) || double.IsInfinity(dd)) { return AppendJsonString(new StringBuilder(), dd.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToString(); }
            return dd.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (value is float ff)
        {
            if (float.IsNaN(ff) || float.IsInfinity(ff)) { return AppendJsonString(new StringBuilder(), ff.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToString(); }
            return ff.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
        // int/long/short/byte/uint 等整型；异常数值兜底写字符串，避免中断整个 dump
        try { return Convert.ToInt64(value).ToString(System.Globalization.CultureInfo.InvariantCulture); }
        catch { return AppendJsonString(new StringBuilder(), value.ToString()).ToString(); }
    }

    // JSON 字符串转义：引号/反斜杠/控制字符；中文等非 ASCII 原样保留
    private static StringBuilder AppendJsonString(StringBuilder sb, string s)
    {
        sb.Append('"');
        if (s != null)
        {
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
        }
        sb.Append('"');
        return sb;
    }

    private static Assembly? ResolveTargetAssembly(string targetExe)
    {
        // 优先按路径精确匹配已加载程序集
        var full = Path.GetFullPath(targetExe);
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (string.Equals(asm.Location, full, StringComparison.OrdinalIgnoreCase)) return asm;
            }
            catch { }
        }
        // 回退：入口程序集
        try { return Assembly.GetEntryAssembly(); } catch { }
        return null;
    }

    private static bool IsBasic(Type t)
    {
        if (t.IsPrimitive || t.IsEnum) return true;
        return t == typeof(string) || t == typeof(decimal)
            || t == typeof(DateTime) || t == typeof(DateTimeOffset)
            || t == typeof(TimeSpan) || t == typeof(Guid);
    }
}

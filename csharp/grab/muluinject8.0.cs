#:property TargetFramework=net8.0-windows
#:property UseWPF=true
#:property PublishTrimmed=false
#:property PublishAot=false
#:property AllowUnsafeBlocks=true
#:property LangVersion=latest
#:property Nullable=disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.WriteLine("usage: dotnet run run_install.cs -- <targetExe>");
    return;
}
var targetExe = Path.GetFullPath(args[0]);
if (!File.Exists(targetExe))
{
    Console.WriteLine("target not found: " + targetExe);
    return;
}

// 自身托管程序集作 startup hook（apphost .exe 改取同目录 .dll）
var selfAsm = typeof(StartupHook).Assembly.Location;
if (Path.GetExtension(selfAsm).Equals(".exe", StringComparison.OrdinalIgnoreCase))
{
    var alt = Path.ChangeExtension(selfAsm, ".dll");
    if (File.Exists(alt)) selfAsm = alt;
}

var psi = new ProcessStartInfo(targetExe)
{
    WorkingDirectory = Path.GetDirectoryName(targetExe)!,
    UseShellExecute = false,
    CreateNoWindow = true,
};
psi.Environment["DOTNET_STARTUP_HOOKS"] = selfAsm;
Process.Start(psi);
Console.WriteLine("injected: " + selfAsm);

public class StartupHook
{
    public static async void Initialize()
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        // 注入前：等待主窗口出现（已创建、已加载、可见）
        var p = Process.GetCurrentProcess();
        var time = DateTime.Now;
        while ((DateTime.Now - time).TotalSeconds < 10)
        {
            p.Refresh();
            if (p.HasExited) return;
            if (p.MainWindowHandle != IntPtr.Zero) break;
            await Task.Delay(100);
        }
        await Task.Delay(1000);

        // 回到 UI 线程再取 Application.Current（hook 早期 await 后可能切线程取到 null）
        await dispatcher.Invoke(async () =>
        {
            var app = Application.Current;
            if (app is null) return;
            var dumpTarget = Environment.GetCommandLineArgs()[0];
            StaticFieldDumper.Dump(dumpTarget); // 需访问 WPF UI 对象，必须在 UI 线程执行
            var mw = app.MainWindow;
            var mi = mw.GetType().GetMethod("ExtractGameAsync",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (mi != null)
            {
                var t = (Task)mi.Invoke(mw, null);
                await t;
            }
        });
    }
}

// 注入成功后：枚举目标程序集内定义的类型的静态字段/常量，并从 Application.Current、
// 窗口实例与 WPF 视图树向下枚举实例对象图，序列化保存到目标 exe 目录
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
        public Assembly TargetAsm;
    }

    public static void Dump(string targetExe)
    {
        try
        {
            // 参数兜底：GetCommandLineArgs 可能为空/非 exe 路径
            if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe))
            {
                try { targetExe = Process.GetCurrentProcess().MainModule?.FileName; } catch { targetExe = null; }
            }
            var dir = Path.GetDirectoryName(targetExe);

            var ctx = new DumpContext { TargetAsm = ResolveTargetAssembly(targetExe) };

            // 顶层 JSON 对象：Application.Current / Windows / 程序集静态常量 三段
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 保留中文等非 ASCII 字符
            }))
            {
                w.WriteStartObject();

                if (ctx.TargetAsm == null)
                {
                    w.WriteString("error", "目标程序集未定位: " + targetExe);
                }
                else
                {
                    Application app;
                    try { app = Application.Current; } catch { app = null; }
                    Type appType = null;
                    try { appType = app?.GetType(); } catch { }

                    // ---- 1. Application.Current：静态/常量优先，再实例图 ----
                    w.WritePropertyName("Application.Current");
                    if (app != null)
                    {
                        w.WriteStartObject();
                        // 1a. Application.Current 具体类型（App 子类）的静态/常量，优先展示
                        DumpTypeStaticsJson(w, ctx, "Application.Current", appType, false);
                        // 1b. Application.Current 实例图成员
                        WriteMembers(w, ctx, "Application.Current", app, 0);
                        w.WriteEndObject();
                    }
                    else w.WriteNullValue();                    

                    // ---- 2. 程序集静态/常量：Application 不可达的值（按类型路径输出）----
                    w.WritePropertyName("程序集静态/常量（Application 不可达）");
                    w.WriteStartObject();
                    Type[] types;
                    try { types = ctx.TargetAsm.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                    catch { types = new Type[0]; }
                    foreach (var type in types)
                    {
                        if (type.Assembly != ctx.TargetAsm) continue;
                        if (appType != null && type == appType) continue; // App 子类静态/常量已在 Application.Current 段优先展示
                        DumpTypeStaticsJson(w, ctx, type.FullName, type, true);
                    }
                    w.WriteEndObject();
                }

                w.WriteEndObject();
            }

            // 输出到目标 exe 目录，JSON 格式化文本
            var file = Path.Combine(dir, Path.GetFileNameWithoutExtension(targetExe) + "_static_fields.json");
            File.WriteAllText(file, Encoding.UTF8.GetString(ms.ToArray()), new UTF8Encoding(false));
        }
        catch
        {
        }
    }

    // 枚举类型静态/常量字段，写入当前已打开的 JSON 对象上下文（复用全局 Visited）
    // fullPathKey=false：key 用字段名（Application.Current 段）；true：key 用 类型.字段（第三段）
    private static void DumpTypeStaticsJson(Utf8JsonWriter w, DumpContext ctx, string ownerPath, Type type, bool fullPathKey)
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
            if(!IsValidValue(ctx,value,0)) continue;
            w.WritePropertyName(fullPathKey ? (type.FullName + "." + f.Name) : f.Name);
            WriteValue(w, ctx, ownerPath + "." + f.Name, value, 0);
        }
    }

    private static bool IsValidValue(DumpContext ctx,object value,int depth)
    {
        if (value is null || depth >= MaxDepth) { return false; }
        var vt = value.GetType();
        if(value is System.Collections.IEnumerable) return true;
        if (vt.Assembly != ctx.TargetAsm) { return false; }
        if (!ctx.Visited.Add(value)) { return false; }
        return true;
    }

    // 递归写入一个值：基本类型→标量；集合/字典/可枚举→数组/对象；目标程序集对象→成员对象
    private static void WriteValue(Utf8JsonWriter w, DumpContext ctx, string path, object value, int depth)
    {
        if (value == null) { w.WriteNullValue(); return; } // null 保留为 JSON null

        var vt = value.GetType();
        if (IsBasic(vt)) { WriteBasicValue(w, value); return; }
        // 集合/字典/可枚举：展开为 JSON 数组/对象
        if (TryWriteCollection(w, ctx, path, value, depth)) return;
        // 非基本、非集合：仅当定义在目标程序集内、未越深度、无循环引用时才展开
        if (vt.Assembly != ctx.TargetAsm || depth >= MaxDepth) { w.WriteStringValue(""); return; }

        w.WriteStartObject();
        WriteMembers(w, ctx, path, value, depth + 1);
        w.WriteEndObject();
    }

    // 将对象的实例字段/属性作为 JSON 成员写入当前已打开的对象上下文（静态/常量已通过类型枚举）
    private static void WriteMembers(Utf8JsonWriter w, DumpContext ctx, string path, object value, int depth)
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
            if(!IsValidValue(ctx,v,depth)) continue;
            w.WritePropertyName(f.Name);
            WriteValue(w, ctx, path + "." + f.Name, v, depth);
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
            if(!IsValidValue(ctx,v,depth)) continue;
            w.WritePropertyName(p.Name);
            WriteValue(w, ctx, path + "." + p.Name, v, depth);
        }
    }

    // 集合/字典/可枚举类型：数组→JSON 数组，字典→JSON 对象（键字符串化），可枚举→JSON 数组
    private static bool TryWriteCollection(Utf8JsonWriter w, DumpContext ctx, string path, object value, int depth)
    {
        var vt = value.GetType();
        if (vt.IsArray)
        {
            var arr = (Array)value;
            int n = Math.Min(arr.Length, MaxItems);
            w.WriteStartArray();
            for (int i = 0; i < n; i++)
            {
                object e;
                try { e = arr.GetValue(i); } catch { continue; }
                if(!IsValidValue(ctx,e,depth)) continue;
                WriteValue(w, ctx, path + "[" + i + "]", e, depth);
            }
            w.WriteEndArray();
            return true;
        }
        if (value is System.Collections.IList list)
        {
            int n = Math.Min(list.Count, MaxItems);
            w.WriteStartArray();
            for (int i = 0; i < n; i++)
            {
                object e;
                try { e = list[i]; } catch { continue; }
                if(!IsValidValue(ctx,e,depth)) continue;
                WriteValue(w, ctx, path + "[" + i + "]", e, depth);
            }
            w.WriteEndArray();
            return true;
        }
        if (value is System.Collections.IDictionary dict)
        {
            int n = 0;
            w.WriteStartObject();
            foreach (System.Collections.DictionaryEntry e in dict)
            {
                if (n++ >= MaxItems) break;                
                if(!IsValidValue(ctx,e.Value,depth)) continue;       
                var key = e.Key == null ? "?" : e.Key.ToString();
                w.WritePropertyName(key);
                WriteValue(w, ctx, path + "[\"" + key + "\"]", e.Value, depth);
            }
            w.WriteEndObject();
            return true;
        }
        if (value is System.Collections.IEnumerable en)
        {
            int n = 0;
            w.WriteStartArray();
            foreach (var e in en)
            {
                if (n >= MaxItems) break;
                if(!IsValidValue(ctx,e,depth)) continue;
                WriteValue(w, ctx, path + "[" + n + "]", e, depth);
                n++;
            }
            w.WriteEndArray();
            return true;
        }
        return false;
    }

    // 基本类型写入 JSON 标量：字符串/字符/布尔/数值/日期/枚举/Guid
    private static void WriteBasicValue(Utf8JsonWriter w, object value)
    {
        if (value is string s) { w.WriteStringValue(s); return; }
        if (value is char c) { w.WriteStringValue(c.ToString()); return; }
        if (value is bool b) { w.WriteBooleanValue(b); return; }
        if (value is DateTime dt) { w.WriteStringValue(dt.ToString("yyyy-MM-dd HH:mm:ss.fff")); return; }
        if (value is DateTimeOffset dto) { w.WriteStringValue(dto.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")); return; }
        if (value is TimeSpan ts) { w.WriteStringValue(ts.ToString()); return; }
        if (value is Guid g) { w.WriteStringValue(g.ToString()); return; }
        if (value is Enum) { w.WriteStringValue(value.ToString()); return; }
        if (value is decimal dc) { w.WriteNumberValue(dc); return; }
        if (value is double dd)
        {
            if (double.IsNaN(dd) || double.IsInfinity(dd)) { w.WriteStringValue(dd.ToString()); return; }
            w.WriteNumberValue(dd); return;
        }
        if (value is float ff)
        {
            if (float.IsNaN(ff) || float.IsInfinity(ff)) { w.WriteStringValue(ff.ToString()); return; }
            w.WriteNumberValue(ff); return;
        }
        // int/long/short/byte/uint 等整型；异常数值兜底写字符串，避免中断整个 dump
        try { w.WriteNumberValue(Convert.ToInt64(value)); }
        catch { w.WriteStringValue(value.ToString()); }
    }

    private static Assembly ResolveTargetAssembly(string targetExe)
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

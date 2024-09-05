using System.Text;
using System.Text.Json.Serialization;

namespace mulu;


public class Game
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? MMSS { get; set; }
    public string? Describe { get; set; }
    public string? RLzz { get; set; }
    public string? Types { get; set; }
    public string? YXBB { get; set; }
    public string? Addr1 { get; set; }
    public string? Addr2 { get; set; }
    public string? Addr3 { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented =true)]
[JsonSerializable(typeof(Sys_content_version))]
[JsonSerializable(typeof(Sys_content_more))]
[JsonSerializable(typeof(Game[]))]
internal partial class SYS : JsonSerializerContext
{
}
public class Sys_content_more
{
    public long Id { get; set; }

    public string? BH { get; set; }

    public string? MM { get; set; }

    public string? Name1 { get; set; }

    public string? Name2 { get; set; }

    public string? BiaoQ { get; set; }

    public string? XingJ { get; set; }

    public string? MageA { get; set; }

    public string? RongL { get; set; }

    public string? RiQ { get; set; }

    public string? Category { get; set; }
}
public class Sys_content_version
{
    public string? Version { get; set; }

    public Sys_content_more[]? Content { get; set; }
}
public class Ini
{

    public static Encoding Encoding { get; set; } = Encoding.GetEncoding("gbk");

    public Dictionary<string, Dictionary<string, object>> Data { get; set; } = new ();

    public static Ini ParseString(string context)
    {
        Ini ini = new();
        var array = context.Split('\n');
        string? text = null;
        foreach (string? text2 in array)
        {
            if (string.IsNullOrEmpty(text2))
            {
                continue;
            }
            string text3 = text2.Trim();
            if (IsAnnotation(text3))
            {
                continue;
            }
            if (text3.StartsWith('[') && text3.EndsWith(']') && text3.Length >= 3)
            {
                text = text3[1..^1];
                continue;
            }
            int num = text3.IndexOf('=');
            if (num > 0 && num + 1 < text3.Length && text != null)
            {
                string key = text3[..num].Trim();
                string value = text3[(num + 1)..].Trim();
                ini.Add(text, key, value);
            }
        }
        return ini;
    }

    private string? GetString(string section, string key)
    {
        //Discarded unreachable code: IL_0005
        //IL_0006: Incompatible stack heights: 0 vs 1
        try
        {
            return Data[section][key].ToString();
        }
        catch
        {
            //Console.WriteLine("获取值失败:section = " + section + ", key = " + key + " " + ex.Message);
            return null;
        }
    }

    public string GetStringValue(string section, string key, string defaultValue = "")
    {
        //Discarded unreachable code: IL_0005
        //IL_0006: Incompatible stack heights: 0 vs 1
        string? text = GetString(section, key);
        if (string.IsNullOrEmpty(text))
        {
            text = defaultValue;
        }
        return text;
    }

    public bool GetBooleanValue(string section, string key, bool defaultValue)
    {
        //Discarded unreachable code: IL_0005
        //IL_0006: Incompatible stack heights: 0 vs 1
        if (bool.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public int GetIntegerValue(string section, string key, int defaultValue = 0)
    {
        //Discarded unreachable code: IL_0005
        //IL_0006: Incompatible stack heights: 0 vs 1
        if (int.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public double GetDoubleValue(string section, string key, double defaultValue = 0.0)
    {
        //Discarded unreachable code: IL_0005
        //IL_0006: Incompatible stack heights: 0 vs 1
        if (double.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public float GetFloatValue(string section, string key, float defaultValue = 0f)
    {
        //Discarded unreachable code: IL_0005
        //IL_0006: Incompatible stack heights: 0 vs 1
        if (float.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public short GetShortValue(string section, string key, short defaultValue = 0)
    {
        //Discarded unreachable code: IL_0005
        //IL_0006: Incompatible stack heights: 0 vs 1
        if (short.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public long GetLongValue(string section, string key, long defaultValue = 0L)
    {
        //Discarded unreachable code: IL_0005
        //IL_0006: Incompatible stack heights: 0 vs 1
        if (long.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public byte GetByteValue(string section, string key, byte defaultValue = 0)
    {
        //Discarded unreachable code: IL_0005
        //IL_0006: Incompatible stack heights: 0 vs 1
        if (byte.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    private static bool IsAnnotation(string line)
    {
        if (line != null && line.Length != 0 && !line.StartsWith(';'))
        {
            return line.StartsWith('#');
        }
        return true;
    }

    public void Add(string section, string key, object value)
    {
        if (!Data.ContainsKey(section) || Data[section] == null)
        {
            Data[section] = new Dictionary<string, object>();
        }
        if (value != null && section != null && key != null)
        {
            Data[section][key] = value;
        }
    }

    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (KeyValuePair<string, Dictionary<string, object>> datum in Data)
        {
            stringBuilder.Append("[").Append(datum.Key).Append("]\r\n");
            foreach (KeyValuePair<string, object> item in datum.Value)
            {
                stringBuilder.Append(item.Key).Append(" = ").Append(item.Value)
                    .Append("\r\n");
            }
            stringBuilder.Append("\r\n");
        }
        return stringBuilder.ToString();
    }
}
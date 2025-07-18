using System.Diagnostics;
using System.Xml.Serialization;


var s = new XmlSerializer(typeof(Manifest[]),new XmlRootAttribute { ElementName = "ModLinks", Namespace = "https://github.com/HollowKnight-Modding/HollowKnight.ModLinks/HollowKnight.ModManager" });
var types = (s.Deserialize(File.OpenText(@"ModLinks.xml")) as Manifest[])!;
var kv = types.Select(s => (s.Name, Link: s.Link ?? s.Links?.Windows??string.Empty)).ToArray();
var lines = kv.Select(s=>$"<li>{s.Name} <a href=\"{s.Link.Replace("https://lgithub.xyz/", "https://github.com/")}\">下载链接1</a> <a href=\"{s.Link.Replace("https://github.com/", "https://lgithub.xyz/")}\">下载链接2</a></li>");

File.WriteAllText("ModLinks.html", $"<ul>{string.Join("",lines)}</ul>");

public class Links
{
    public string Linux { get; set; } = null!;
    public string Mac { get; set; } = null!;
    public string Windows { get; set; } = null!;
}

[DebuggerDisplay("Name={Name},Link={Link},Links={Links}")]
public class Manifest
{
    public string Name { get; set; } = null!;
    public string? Link { get; set; }
    public Links? Links { get; set; }
}


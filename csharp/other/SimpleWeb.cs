#:sdk Microsoft.NET.Sdk.Web 
using System.Runtime.Versioning;
[assembly: SupportedOSPlatform("windows")]
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
{
    Args = args
});
builder.WebHost.UseHttpSys();
builder.Logging.AddSimpleConsole(s =>
{
    s.SingleLine = true;
    s.TimestampFormat = "MM-dd HH:mm:ss ";
});
var app = builder.Build();
app.UseFileServer();
app.Run();
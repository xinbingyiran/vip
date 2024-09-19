using Iot.Device.DhcpServer;
using nanoFramework.Networking;
using nanoFramework.WebServer;
using System;
using System.Collections;
using System.Device.Gpio;
using System.Device.Wifi;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Web;


namespace NFApp1
{
    public class Person
    {
        public string First { get; set; }
        public string Last { get; set; }
    }

    public class ControllerPerson
    {
        private static ArrayList _persons = new ArrayList();
        private static object _lock = new object();

        [Route("Person")]
        [Method("GET")]
        public void Get(WebServerEventArgs e)
        {
            var ret = "[";
            lock (_lock)
            {
                foreach (var person in _persons)
                {
                    var per = (Person)person;
                    ret += $"{{\"First\"=\"{per.First}\",\"Last\"=\"{per.Last}\"}},";
                }
            }
            if (ret.Length > 1)
            {
                ret = ret.Substring(0, ret.Length - 1);
            }
            ret += "]";
            e.Context.Response.ContentType = "text/html";
            e.Context.Response.ContentLength64 = ret.Length;
            Program.OutPutString(e.Context, ret);
        }

        [Route("Person/Add")]
        [Method("POST")]
        public void AddPost(WebServerEventArgs e)
        {
            // Get the param from the body
            byte[] buff = new byte[e.Context.Request.ContentLength64];
            e.Context.Request.InputStream.Read(buff, 0, buff.Length);
            string rawData = new string(Encoding.UTF8.GetChars(buff));
            rawData = $"?{rawData}";
            AddPerson(e.Context, rawData);
        }

        [Route("Person/Add")]
        [Method("GET")]
        public void AddGet(WebServerEventArgs e)
        {
            AddPerson(e.Context, e.Context.Request.RawUrl);
        }

        private void AddPerson(HttpListenerContext context, string url)
        {
            var parameters = WebServer.DecodeParam(url);
            Person person = new Person();
            foreach (var param in parameters)
            {
                if (param.Name.ToLower() == "first")
                {
                    person.First = param.Value;
                }
                else if (param.Name.ToLower() == "last")
                {
                    person.Last = param.Value;
                }
            }
            if ((person.Last != string.Empty) && (person.First != string.Empty))
            {
                lock (_lock)
                {
                    _persons.Add(person);
                }
                Program.OutPutCode(context, HttpStatusCode.Accepted);
            }
            else
            {
                Program.OutPutCode(context, HttpStatusCode.BadRequest);
            }
        }
    }

    public class FileController
    {
        [Route("File")]
        [Method("GET")]
        public void ManagerFile(WebServerEventArgs e)
        {
            string html = @"<!DOCTYPE html>
<html lang=""zh"">

<head>
  <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"" />
  <meta path=""viewport""
    content=""width=device-width,initial-scale=1.0,minimum-scale=0.5,maximum-scale=2,user-scalable=1"">
  <title>文件上传</title>
</head>

<body>
  <span id=""message""></span><br />
  <input id=""path"" type=""text"" value=""/"" />
  <input type=""button"" onclick=""load()"" value=""获取"" /><br />
  <span id=""list""></span><br />
  <input type=""file"" path=""file"" multiple=""multiple"" id=""files"" onchange=""choosefile()"" /><br />
  <span id=""name""></span><br />
  <input type=""button"" onclick=""upload()"" value=""上传"" />
  <script type=""text/javascript"">
    const baseUrl = ""."";
    const items = [];
    function showMessage(msg) {
      document.querySelector('#message').innerText = msg;
    }
    async function choosefile() {
      items.splice(0, items.length);
      const fileList = document.querySelector('#files').files;
      const nameList = document.querySelector('#name');
      [...nameList.childNodes].forEach(s => nameList.removeChild(s));
      for (let i = 0; i < fileList.length; i++) {
        const fi = fileList[i];
        const buffer = await new Promise(function (resolve, reject) {
          let reader = new FileReader();
          reader.onload = function (e) {
            resolve(e.target.result);
          }
          reader.readAsArrayBuffer(fi);
        });
        const div = document.createElement('div');
        div.appendChild(document.createTextNode(fi.name + ' - ' + fi.size));
        const statusNode = document.createTextNode(""准备上传！"");
        div.appendChild(statusNode);
        nameList.appendChild(div);
        items.push({
          name: fi.name,
          type: fi.type,
          size: fi.size,
          buffer: buffer,
          statusNode: statusNode
        });
      }

    }
    async function load() {
      showMessage(""正在获取文件列表..."");
      const path = document.querySelector('#path').value;
      const resp = await fetch(baseUrl + '/File/List?path=' + path);
      if (resp.status !== 200) {
        showMessage(`获取文件列表失败：${resp.statusText}[${resp.status}]`);
        return;
      }
      const list = document.querySelector('#list');
      [...list.childNodes].forEach(s => list.removeChild(s));
      (await resp.json()).forEach(element => {
        const div = document.createElement('div');
        div.appendChild(document.createTextNode(element.name + ' - ' + element.size));
        if (!element.name.endsWith('/')) {
          const down = document.createElement('a');
          down.href = baseUrl + '/File/Download?path=' + path.trimEnd('/') + '/' + element.name;
          down.text = '下载';
          down.style.marginLeft = '10px';
          div.appendChild(down);
        }
        const del = document.createElement('a');
        del.href = ""javascript:void(0)"";
        del.text = '删除';
        del.style.marginLeft = '10px';
        del.onclick = async function () {
          const deleteType = element.name.endsWith('/') ? '目录' : '文件';
          showMessage(`正在删除${deleteType}...`);
          const resp = await fetch(baseUrl + '/File/Delete?path=' + encodeURIComponent(path.trimEnd('/') + '/' + element.name), {method: 'POST'});
          if (resp.status !== 200) {
            showMessage(`删除${deleteType}失败：${resp.statusText}[${resp.status}]`);
            return;
          }
          showMessage(`删除${deleteType}完成。`);
        }
        div.appendChild(del);
        list.appendChild(div);
      });
      showMessage(""获取文件列表完成。"");
    }
    async function upload() {
      showMessage(""正在上传..."");
      for (const item of items) {
        item.statusNode.textContent = ""上传中。。。"";
        const resp = await fetch(baseUrl + '/File/Add?path=' + encodeURIComponent(document.querySelector('#path').value.trimEnd('/') + '/' + item.name), {
          method: 'POST',
          body: item.buffer
        });
        if (resp.status !== 200) {
          item.statusNode.textContent = `上传文件失败：${resp.statusText}[${resp.status}]`;
        }
        else {
          item.statusNode.textContent = ""上传完成！"";
        }
      }
      showMessage(""上传文件完成。"");
    }
  </script>
</body>

</html>";
            Program.OutPutString(e.Context, html);
        }

        [Route("File/Add")]
        [Method("POST")]
        public void AddPost(WebServerEventArgs e)
        {
            var rawUrl = e.Context.Request.RawUrl;
            var paramsUrl = WebServer.DecodeParam(rawUrl);
            var fileName = "";
            foreach (var para in paramsUrl)
            {
                if (string.Equals(para.Name, "path"))
                {
                    fileName = HttpUtility.UrlDecode(para.Value);
                }
            }
            if (string.IsNullOrEmpty(fileName))
            {
                Program.OutPutCode(e.Context, HttpStatusCode.BadRequest);
                return;
            }
            var fi = new FileInfo("I:/" + fileName.Trim('/'));
            var di = fi.Directory;
            if (!di.Exists)
            {
                di.Create();
            }
            if (fi.Exists)
            {
                fi.Delete();
            }
            using var stream = new FileStream(fi.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            byte[] array = new byte[2048];
            int count;
            var write = (int)e.Context.Request.ContentLength64;
            while (write > 0 && (count = e.Context.Request.InputStream.Read(array, 0, write > array.Length ? array.Length : write)) > 0)
            {
                stream.Write(array, 0, count);
                write -= count;
            }
            stream.Close();
            Program.OutPutCode(e.Context, HttpStatusCode.OK);
        }

        [Route("File/Delete")]
        [Method("POST")]
        public void DeletePost(WebServerEventArgs e)
        {
            var rawUrl = e.Context.Request.RawUrl;
            var paramsUrl = WebServer.DecodeParam(rawUrl);
            var fileName = "";
            foreach (var para in paramsUrl)
            {
                if (string.Equals(para.Name, "path"))
                {
                    fileName = HttpUtility.UrlDecode(para.Value);
                }
            }
            if (string.IsNullOrEmpty(fileName))
            {
                Program.OutPutCode(e.Context, HttpStatusCode.BadRequest);
                return;
            }
            FileSystemInfo fi = fileName.EndsWith("/") ? new DirectoryInfo("I:/" + fileName.Trim('/')) : new FileInfo("I:/" + fileName.Trim('/'));
            if (!fi.Exists)
            {
                Program.OutPutCode(e.Context, HttpStatusCode.NotFound);
                return;
            }
            fi.Delete();
            Program.OutPutCode(e.Context, HttpStatusCode.OK);
        }

        [Route("File/List")]
        [Method("GET")]
        public void GetFiles(WebServerEventArgs e)
        {
            var rawUrl = e.Context.Request.RawUrl;
            var paramsUrl = WebServer.DecodeParam(rawUrl);
            var fileName = "";
            foreach (var para in paramsUrl)
            {
                if (string.Equals(para.Name, "path"))
                {
                    fileName = HttpUtility.UrlDecode(para.Value);
                }
            }
            if (string.IsNullOrEmpty(fileName))
            {
                Program.OutPutCode(e.Context, HttpStatusCode.BadRequest);
                return;
            }
            var di = new DirectoryInfo("I:/" + fileName.Trim('/'));
            if (!di.Exists)
            {
                Program.OutPutCode(e.Context, HttpStatusCode.NotFound);
                return;
            }
            var dis = di.GetDirectories();
            var fis = di.GetFiles();
            var sb = new StringBuilder("[");
            foreach (var d in dis)
            {
                sb.Append($"{{\"name\":\"{d.Name}/\", \"size\":0}},");
            }
            foreach (var f in fis)
            {
                sb.Append($"{{\"name\":\"{f.Name}\", \"size\":{f.Length}}},");
            }
            if (sb.Length > 1)
            {
                sb[sb.Length - 1] = ']';
            }
            else
            {
                sb.Append(']');
            }
            Program.OutPutString(e.Context, sb.ToString());
        }

        [Route("File/Download")]
        [Method("GET")]
        public void DownloadFile(WebServerEventArgs e)
        {
            var rawUrl = e.Context.Request.RawUrl;
            var paramsUrl = WebServer.DecodeParam(rawUrl);
            var fileName = "";
            foreach (var para in paramsUrl)
            {
                if (string.Equals(para.Name, "path"))
                {
                    fileName = HttpUtility.UrlDecode(para.Value);
                }
            }
            if (string.IsNullOrEmpty(fileName))
            {
                Program.OutPutCode(e.Context, HttpStatusCode.BadRequest);
                return;
            }
            var fi = new FileInfo("I:/" + fileName.Trim('/'));
            if (!fi.Exists)
            {
                Program.OutPutCode(e.Context, HttpStatusCode.NotFound);
                return;
            }
            e.Context.Response.Headers.Add("Content-Disposition", $"attachment;filename={HttpUtility.UrlEncode(fi.Name)}");
            Program.OutPutFile(e.Context,fi);
        }
    }

    public class ControllerTest
    {
        [Route("test"), Route("Test2"), Route("tEst42"), Route("TEST")]
        [CaseSensitive]
        [Method("GET")]
        public void RoutePostTest(WebServerEventArgs e)
        {
            string route = $"The route asked is {e.Context.Request.RawUrl.TrimStart('/').Split('/')[0]}";
            e.Context.Response.ContentType = "text/plain";
            Program.OutPutString(e.Context, route);
        }

        [Route("test/any")]
        public void RouteAnyTest(WebServerEventArgs e)
        {
            Program.OutPutCode(e.Context, HttpStatusCode.OK);
        }

        [Route("urlencode")]
        public void UrlEncode(WebServerEventArgs e)
        {
            var rawUrl = e.Context.Request.RawUrl;
            var paramsUrl = WebServer.DecodeParam(rawUrl);
            string ret = "Parameters | Encoded | Decoded";
            foreach (var param in paramsUrl)
            {
                ret += $"{param.Name} | ";
                ret += $"{param.Value} | ";
                // Need to wait for latest version of System.Net
                // See https://github.com/nanoframework/lib-nanoFramework.System.Net.Http/blob/develop/nanoFramework.System.Net.Http/Http/System.Net.HttpUtility.cs
                ret += $"{HttpUtility.UrlDecode(param.Value)}";
                ret += "\r\n";
            }
            Program.OutPutString(e.Context, ret);
        }
    }

    [Authentication("Basic:user password")]
    class ControllerAuth
    {
        [Route("authbasic")]
        public void Basic(WebServerEventArgs e)
        {
            Program.OutPutCode(e.Context, HttpStatusCode.OK);
        }

        [Route("authbasicspecial")]
        [Authentication("Basic:user2 password")]
        public void Special(WebServerEventArgs e)
        {
            Program.OutPutCode(e.Context, HttpStatusCode.OK);
        }

        [Authentication("ApiKey:superKey1234")]
        [Route("authapi")]
        public void Key(WebServerEventArgs e)
        {
            Program.OutPutCode(e.Context, HttpStatusCode.OK);
        }

        [Route("authnone")]
        [Authentication("None")]
        public void None(WebServerEventArgs e)
        {
            Program.OutPutCode(e.Context, HttpStatusCode.OK);
        }

        [Authentication("ApiKey")]
        [Route("authdefaultapi")]
        public void DefaultApi(WebServerEventArgs e)
        {
            Program.OutPutCode(e.Context, HttpStatusCode.OK);
        }
    }


    public class Program
    {

        public static void OutPutString(HttpListenerContext context, string content)
        {
            CheckCors(context);
            WebServer.OutPutStream(context.Response, content);
        }
        public static void OutPutStream(HttpListenerContext context, Stream stream)
        {
            CheckCors(context);
            stream.CopyTo(context.Response.OutputStream);
        }
        public static void OutPutCode(HttpListenerContext context, HttpStatusCode code)
        {
            CheckCors(context);
            WebServer.OutputHttpCode(context.Response, code);
        }

        public static void OutPutFile(HttpListenerContext context, FileInfo fi)
        {
            var response = context.Response;
            response.ContentType = GetContentType(fi.Extension);
            response.ContentLength64 = fi.Length;
            using var fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            OutPutStream(context, fs);
        }
        public static void CheckCors(HttpListenerContext context)
        {
            var hosts = context.Request.Headers.GetValues("Host");
            if (hosts == null || hosts.Length != 1)
            {
                return;
            }
            var hostWidthProtocol = "http://" + hosts[0];

            var values = context.Request.Headers.GetValues("Origin");
            if (values is null || values.Length != 1)
            {
                return;
            }
            if (values[0] == hostWidthProtocol)
            {
                return;
            }
            context.Response.Headers.Add("Access-Control-Allow-Origin", values[0]);


            values = context.Request.Headers.GetValues("Access-Control-Request-Method");
            if (values is not null && values.Length == 1)
            {
                if (values[0] == context.Request.HttpMethod)
                {
                    context.Response.Headers.Add("Access-Control-Allow-Methods", values[0]);
                }
                else
                {
                    context.Response.Headers.Add("Access-Control-Allow-Methods", $"{values[0]}, {context.Request.HttpMethod}");
                }
            }
            values = context.Request.Headers.GetValues("Access-Control-Request-Headers");
            if (values is not null && values.Length > 0)
            {
                StringBuilder sb = null;
                foreach (var value in values)
                {
                    if (sb == null)
                    {
                        sb = new StringBuilder(value);
                    }
                    else
                    {
                        sb.Append(" ,");
                        sb.Append(value);
                    }
                }
                context.Response.Headers.Add("Access-Control-Allow-Headers", sb.ToString());
            }
        }
        static readonly GpioController _controller = new GpioController();
        static readonly string apiKey = "apiKey";
        static string GetContentType(string ext) => ext switch
        {
            ".323" => "text/h323",
            ".3g2" => "video/3gpp2",
            ".3gp2" => "video/3gpp2",
            ".3gp" => "video/3gpp",
            ".3gpp" => "video/3gpp",
            ".aac" => "audio/aac",
            ".aaf" => "application/octet-stream",
            ".aca" => "application/octet-stream",
            ".accdb" => "application/msaccess",
            ".accde" => "application/msaccess",
            ".accdt" => "application/msaccess",
            ".acx" => "application/internet-property-stream",
            ".adt" => "audio/vnd.dlna.adts",
            ".adts" => "audio/vnd.dlna.adts",
            ".afm" => "application/octet-stream",
            ".ai" => "application/postscript",
            ".aif" => "audio/x-aiff",
            ".aifc" => "audio/aiff",
            ".aiff" => "audio/aiff",
            ".appcache" => "text/cache-manifest",
            ".application" => "application/x-ms-application",
            ".art" => "image/x-jg",
            ".asd" => "application/octet-stream",
            ".asf" => "video/x-ms-asf",
            ".asi" => "application/octet-stream",
            ".asm" => "text/plain",
            ".asr" => "video/x-ms-asf",
            ".asx" => "video/x-ms-asf",
            ".atom" => "application/atom+xml",
            ".au" => "audio/basic",
            ".avi" => "video/x-msvideo",
            ".axs" => "application/olescript",
            ".bas" => "text/plain",
            ".bcpio" => "application/x-bcpio",
            ".bin" => "application/octet-stream",
            ".bmp" => "image/bmp",
            ".c" => "text/plain",
            ".cab" => "application/vnd.ms-cab-compressed",
            ".calx" => "application/vnd.ms-office.calx",
            ".cat" => "application/vnd.ms-pki.seccat",
            ".cdf" => "application/x-cdf",
            ".chm" => "application/octet-stream",
            ".class" => "application/x-java-applet",
            ".clp" => "application/x-msclip",
            ".cmx" => "image/x-cmx",
            ".cnf" => "text/plain",
            ".cod" => "image/cis-cod",
            ".cpio" => "application/x-cpio",
            ".cpp" => "text/plain",
            ".crd" => "application/x-mscardfile",
            ".crl" => "application/pkix-crl",
            ".crt" => "application/x-x509-ca-cert",
            ".csh" => "application/x-csh",
            ".css" => "text/css",
            ".csv" => "text/csv",
            ".cur" => "application/octet-stream",
            ".dcr" => "application/x-director",
            ".deploy" => "application/octet-stream",
            ".der" => "application/x-x509-ca-cert",
            ".dib" => "image/bmp",
            ".dir" => "application/x-director",
            ".disco" => "text/xml",
            ".dlm" => "text/dlm",
            ".doc" => "application/msword",
            ".docm" => "application/vnd.ms-word.document.macroEnabled.12",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".dot" => "application/msword",
            ".dotm" => "application/vnd.ms-word.template.macroEnabled.12",
            ".dotx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
            ".dsp" => "application/octet-stream",
            ".dtd" => "text/xml",
            ".dvi" => "application/x-dvi",
            ".dvr-ms" => "video/x-ms-dvr",
            ".dwf" => "drawing/x-dwf",
            ".dwp" => "application/octet-stream",
            ".dxr" => "application/x-director",
            ".eml" => "message/rfc822",
            ".emz" => "application/octet-stream",
            ".eot" => "application/vnd.ms-fontobject",
            ".eps" => "application/postscript",
            ".etx" => "text/x-setext",
            ".evy" => "application/envoy",
            ".exe" => "application/vnd.microsoft.portable-executable",
            ".fdf" => "application/vnd.fdf",
            ".fif" => "application/fractals",
            ".fla" => "application/octet-stream",
            ".flr" => "x-world/x-vrml",
            ".flv" => "video/x-flv",
            ".gif" => "image/gif",
            ".gtar" => "application/x-gtar",
            ".gz" => "application/x-gzip",
            ".h" => "text/plain",
            ".hdf" => "application/x-hdf",
            ".hdml" => "text/x-hdml",
            ".hhc" => "application/x-oleobject",
            ".hhk" => "application/octet-stream",
            ".hhp" => "application/octet-stream",
            ".hlp" => "application/winhlp",
            ".hqx" => "application/mac-binhex40",
            ".hta" => "application/hta",
            ".htc" => "text/x-component",
            ".htm" => "text/html",
            ".html" => "text/html",
            ".htt" => "text/webviewhtml",
            ".hxt" => "text/html",
            ".ical" => "text/calendar",
            ".icalendar" => "text/calendar",
            ".ico" => "image/x-icon",
            ".ics" => "text/calendar",
            ".ief" => "image/ief",
            ".ifb" => "text/calendar",
            ".iii" => "application/x-iphone",
            ".inf" => "application/octet-stream",
            ".ins" => "application/x-internet-signup",
            ".isp" => "application/x-internet-signup",
            ".IVF" => "video/x-ivf",
            ".jar" => "application/java-archive",
            ".java" => "application/octet-stream",
            ".jck" => "application/liquidmotion",
            ".jcz" => "application/liquidmotion",
            ".jfif" => "image/pjpeg",
            ".jpb" => "application/octet-stream",
            ".jpe" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".jpg" => "image/jpeg",
            ".js" => "text/javascript",
            ".json" => "application/json",
            ".jsx" => "text/jscript",
            ".latex" => "application/x-latex",
            ".lit" => "application/x-ms-reader",
            ".lpk" => "application/octet-stream",
            ".lsf" => "video/x-la-asf",
            ".lsx" => "video/x-la-asf",
            ".lzh" => "application/octet-stream",
            ".m13" => "application/x-msmediaview",
            ".m14" => "application/x-msmediaview",
            ".m1v" => "video/mpeg",
            ".m2ts" => "video/vnd.dlna.mpeg-tts",
            ".m3u" => "audio/x-mpegurl",
            ".m4a" => "audio/mp4",
            ".m4v" => "video/mp4",
            ".man" => "application/x-troff-man",
            ".manifest" => "application/x-ms-manifest",
            ".map" => "text/plain",
            ".markdown" => "text/markdown",
            ".md" => "text/markdown",
            ".mdb" => "application/x-msaccess",
            ".mdp" => "application/octet-stream",
            ".me" => "application/x-troff-me",
            ".mht" => "message/rfc822",
            ".mhtml" => "message/rfc822",
            ".mid" => "audio/mid",
            ".midi" => "audio/mid",
            ".mix" => "application/octet-stream",
            ".mjs" => "text/javascript",
            ".mmf" => "application/x-smaf",
            ".mno" => "text/xml",
            ".mny" => "application/x-msmoney",
            ".mov" => "video/quicktime",
            ".movie" => "video/x-sgi-movie",
            ".mp2" => "video/mpeg",
            ".mp3" => "audio/mpeg",
            ".mp4" => "video/mp4",
            ".mp4v" => "video/mp4",
            ".mpa" => "video/mpeg",
            ".mpe" => "video/mpeg",
            ".mpeg" => "video/mpeg",
            ".mpg" => "video/mpeg",
            ".mpp" => "application/vnd.ms-project",
            ".mpv2" => "video/mpeg",
            ".ms" => "application/x-troff-ms",
            ".msi" => "application/octet-stream",
            ".mso" => "application/octet-stream",
            ".mvb" => "application/x-msmediaview",
            ".mvc" => "application/x-miva-compiled",
            ".nc" => "application/x-netcdf",
            ".nsc" => "video/x-ms-asf",
            ".nws" => "message/rfc822",
            ".ocx" => "application/octet-stream",
            ".oda" => "application/oda",
            ".odc" => "text/x-ms-odc",
            ".ods" => "application/oleobject",
            ".oga" => "audio/ogg",
            ".ogg" => "video/ogg",
            ".ogv" => "video/ogg",
            ".ogx" => "application/ogg",
            ".one" => "application/onenote",
            ".onea" => "application/onenote",
            ".onetoc" => "application/onenote",
            ".onetoc2" => "application/onenote",
            ".onetmp" => "application/onenote",
            ".onepkg" => "application/onenote",
            ".osdx" => "application/opensearchdescription+xml",
            ".otf" => "font/otf",
            ".p10" => "application/pkcs10",
            ".p12" => "application/x-pkcs12",
            ".p7b" => "application/x-pkcs7-certificates",
            ".p7c" => "application/pkcs7-mime",
            ".p7m" => "application/pkcs7-mime",
            ".p7r" => "application/x-pkcs7-certreqresp",
            ".p7s" => "application/pkcs7-signature",
            ".pbm" => "image/x-portable-bitmap",
            ".pcx" => "application/octet-stream",
            ".pcz" => "application/octet-stream",
            ".pdf" => "application/pdf",
            ".pfb" => "application/octet-stream",
            ".pfm" => "application/octet-stream",
            ".pfx" => "application/x-pkcs12",
            ".pgm" => "image/x-portable-graymap",
            ".pko" => "application/vnd.ms-pki.pko",
            ".pma" => "application/x-perfmon",
            ".pmc" => "application/x-perfmon",
            ".pml" => "application/x-perfmon",
            ".pmr" => "application/x-perfmon",
            ".pmw" => "application/x-perfmon",
            ".png" => "image/png",
            ".pnm" => "image/x-portable-anymap",
            ".pnz" => "image/png",
            ".pot" => "application/vnd.ms-powerpoint",
            ".potm" => "application/vnd.ms-powerpoint.template.macroEnabled.12",
            ".potx" => "application/vnd.openxmlformats-officedocument.presentationml.template",
            ".ppam" => "application/vnd.ms-powerpoint.addin.macroEnabled.12",
            ".ppm" => "image/x-portable-pixmap",
            ".pps" => "application/vnd.ms-powerpoint",
            ".ppsm" => "application/vnd.ms-powerpoint.slideshow.macroEnabled.12",
            ".ppsx" => "application/vnd.openxmlformats-officedocument.presentationml.slideshow",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptm" => "application/vnd.ms-powerpoint.presentation.macroEnabled.12",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".prf" => "application/pics-rules",
            ".prm" => "application/octet-stream",
            ".prx" => "application/octet-stream",
            ".ps" => "application/postscript",
            ".psd" => "application/octet-stream",
            ".psm" => "application/octet-stream",
            ".psp" => "application/octet-stream",
            ".pub" => "application/x-mspublisher",
            ".qt" => "video/quicktime",
            ".qtl" => "application/x-quicktimeplayer",
            ".qxd" => "application/octet-stream",
            ".ra" => "audio/x-pn-realaudio",
            ".ram" => "audio/x-pn-realaudio",
            ".rar" => "application/octet-stream",
            ".ras" => "image/x-cmu-raster",
            ".rf" => "image/vnd.rn-realflash",
            ".rgb" => "image/x-rgb",
            ".rm" => "application/vnd.rn-realmedia",
            ".rmi" => "audio/mid",
            ".roff" => "application/x-troff",
            ".rpm" => "audio/x-pn-realaudio-plugin",
            ".rtf" => "application/rtf",
            ".rtx" => "text/richtext",
            ".scd" => "application/x-msschedule",
            ".sct" => "text/scriptlet",
            ".sea" => "application/octet-stream",
            ".setpay" => "application/set-payment-initiation",
            ".setreg" => "application/set-registration-initiation",
            ".sgml" => "text/sgml",
            ".sh" => "application/x-sh",
            ".shar" => "application/x-shar",
            ".sit" => "application/x-stuffit",
            ".sldm" => "application/vnd.ms-powerpoint.slide.macroEnabled.12",
            ".sldx" => "application/vnd.openxmlformats-officedocument.presentationml.slide",
            ".smd" => "audio/x-smd",
            ".smi" => "application/octet-stream",
            ".smx" => "audio/x-smd",
            ".smz" => "audio/x-smd",
            ".snd" => "audio/basic",
            ".snp" => "application/octet-stream",
            ".spc" => "application/x-pkcs7-certificates",
            ".spl" => "application/futuresplash",
            ".spx" => "audio/ogg",
            ".src" => "application/x-wais-source",
            ".ssm" => "application/streamingmedia",
            ".sst" => "application/vnd.ms-pki.certstore",
            ".stl" => "application/vnd.ms-pki.stl",
            ".sv4cpio" => "application/x-sv4cpio",
            ".sv4crc" => "application/x-sv4crc",
            ".svg" => "image/svg+xml",
            ".svgz" => "image/svg+xml",
            ".swf" => "application/x-shockwave-flash",
            ".t" => "application/x-troff",
            ".tar" => "application/x-tar",
            ".tcl" => "application/x-tcl",
            ".tex" => "application/x-tex",
            ".texi" => "application/x-texinfo",
            ".texinfo" => "application/x-texinfo",
            ".tgz" => "application/x-compressed",
            ".thmx" => "application/vnd.ms-officetheme",
            ".thn" => "application/octet-stream",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            ".toc" => "application/octet-stream",
            ".tr" => "application/x-troff",
            ".trm" => "application/x-msterminal",
            ".ts" => "video/vnd.dlna.mpeg-tts",
            ".tsv" => "text/tab-separated-values",
            ".ttc" => "application/x-font-ttf",
            ".ttf" => "application/x-font-ttf",
            ".tts" => "video/vnd.dlna.mpeg-tts",
            ".txt" => "text/plain",
            ".u32" => "application/octet-stream",
            ".uls" => "text/iuls",
            ".ustar" => "application/x-ustar",
            ".vbs" => "text/vbscript",
            ".vcf" => "text/x-vcard",
            ".vcs" => "text/plain",
            ".vdx" => "application/vnd.ms-visio.viewer",
            ".vml" => "text/xml",
            ".vsd" => "application/vnd.visio",
            ".vss" => "application/vnd.visio",
            ".vst" => "application/vnd.visio",
            ".vsto" => "application/x-ms-vsto",
            ".vsw" => "application/vnd.visio",
            ".vsx" => "application/vnd.visio",
            ".vtx" => "application/vnd.visio",
            ".wasm" => "application/wasm",
            ".wav" => "audio/wav",
            ".wax" => "audio/x-ms-wax",
            ".wbmp" => "image/vnd.wap.wbmp",
            ".wcm" => "application/vnd.ms-works",
            ".wdb" => "application/vnd.ms-works",
            ".webm" => "video/webm",
            ".webmanifest" => "application/manifest+json",
            ".webp" => "image/webp",
            ".wks" => "application/vnd.ms-works",
            ".wm" => "video/x-ms-wm",
            ".wma" => "audio/x-ms-wma",
            ".wmd" => "application/x-ms-wmd",
            ".wmf" => "application/x-msmetafile",
            ".wml" => "text/vnd.wap.wml",
            ".wmlc" => "application/vnd.wap.wmlc",
            ".wmls" => "text/vnd.wap.wmlscript",
            ".wmlsc" => "application/vnd.wap.wmlscriptc",
            ".wmp" => "video/x-ms-wmp",
            ".wmv" => "video/x-ms-wmv",
            ".wmx" => "video/x-ms-wmx",
            ".wmz" => "application/x-ms-wmz",
            ".woff" => "application/font-woff",
            ".woff2" => "font/woff2",
            ".wps" => "application/vnd.ms-works",
            ".wri" => "application/x-mswrite",
            ".wrl" => "x-world/x-vrml",
            ".wrz" => "x-world/x-vrml",
            ".wsdl" => "text/xml",
            ".wtv" => "video/x-ms-wtv",
            ".wvx" => "video/x-ms-wvx",
            ".x" => "application/directx",
            ".xaf" => "x-world/x-vrml",
            ".xaml" => "application/xaml+xml",
            ".xap" => "application/x-silverlight-app",
            ".xbap" => "application/x-ms-xbap",
            ".xbm" => "image/x-xbitmap",
            ".xdr" => "text/plain",
            ".xht" => "application/xhtml+xml",
            ".xhtml" => "application/xhtml+xml",
            ".xla" => "application/vnd.ms-excel",
            ".xlam" => "application/vnd.ms-excel.addin.macroEnabled.12",
            ".xlc" => "application/vnd.ms-excel",
            ".xlm" => "application/vnd.ms-excel",
            ".xls" => "application/vnd.ms-excel",
            ".xlsb" => "application/vnd.ms-excel.sheet.binary.macroEnabled.12",
            ".xlsm" => "application/vnd.ms-excel.sheet.macroEnabled.12",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xlt" => "application/vnd.ms-excel",
            ".xltm" => "application/vnd.ms-excel.template.macroEnabled.12",
            ".xltx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
            ".xlw" => "application/vnd.ms-excel",
            ".xml" => "text/xml",
            ".xof" => "x-world/x-vrml",
            ".xpm" => "image/x-xpixmap",
            ".xps" => "application/vnd.ms-xpsdocument",
            ".xsd" => "text/xml",
            ".xsf" => "text/xml",
            ".xsl" => "text/xml",
            ".xslt" => "text/xml",
            ".xsn" => "application/octet-stream",
            ".xtp" => "application/octet-stream",
            ".xwd" => "image/x-xwindowdump",
            ".z" => "application/x-compress",
            ".zip" => "application/x-zip-compressed",
            _ => "application/octet-stream"
        };
        public static void Main()
        {
            new Thread(() =>
            {
                var led = _controller.OpenPin(2, PinMode.Output);
                while (true)
                {
                    led.Toggle();
                    Thread.Sleep(1000);
                }
            }).Start();
            try
            {
                Debug.WriteLine("连接网络");
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.WirelessAP)
                    {
                        const string ip = "192.168.4.1";
                        var config = WirelessAPConfiguration.GetAllWirelessAPConfigurations()[ni.SpecificConfigId];
                        ni.EnableStaticIPv4(ip, "255.255.255.0", ip);
                        config.Options = WirelessAPConfiguration.ConfigurationOptions.AutoStart | WirelessAPConfiguration.ConfigurationOptions.Enable;
                        config.Ssid = $"ESP32_{ni.PhysicalAddress[3]:X2}{ni.PhysicalAddress[4]:X2}{ni.PhysicalAddress[5]:X2}";
                        config.MaxConnections = 4;
                        config.Authentication = System.Net.NetworkInformation.AuthenticationType.Open;
                        config.Password = "";
                        config.SaveConfiguration();
                        var dhcpserver = new DhcpServer
                        {
                            CaptivePortalUrl = $"http://{ip}"
                        };
                        var dhcpInitResult = dhcpserver.Start(IPAddress.Parse(ip), new IPAddress(new byte[] { 255, 255, 255, 0 }));

                    }
                    else if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        const string MySsid = "";
                        const string MyPassword = "";
                        ni.EnableDhcp();
                        var config = Wireless80211Configuration.GetAllWireless80211Configurations()[ni.SpecificConfigId];
                        config.Options = Wireless80211Configuration.ConfigurationOptions.None | Wireless80211Configuration.ConfigurationOptions.SmartConfig;
                        config.Ssid = MySsid;
                        config.Password = MyPassword;
                        config.SaveConfiguration();
                        //CancellationTokenSource cs = new(60000);
                        //if (!WifiNetworkHelper.Reconnect(false, 0, cs.Token))
                        //{
                        //    Debug.WriteLine($"连接异常: {WifiNetworkHelper.Status}.");
                        //    if (WifiNetworkHelper.HelperException != null)
                        //    {
                        //        Debug.WriteLine($"异常信息: {WifiNetworkHelper.HelperException}");
                        //    }
                        //    return;
                        //}
                    }
                }

                foreach (var inf in NetworkInterface.GetAllNetworkInterfaces())
                {
                    Debug.WriteLine($"连接网络成功：{inf.IPv4Address}");
                }
                // Instantiate a new web server on port 80.
                using WebServer server = new WebServer(80, HttpProtocol.Http, new Type[] { typeof(FileController), typeof(ControllerPerson), typeof(ControllerTest), typeof(ControllerAuth) });
                server.CommandReceived += ServerCommandReceived;
                server.Start();
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {

                Debug.WriteLine($"{ex}");
            }
        }
        private static void ServerCommandReceived(object source, WebServerEventArgs e)
        {
            try
            {
                var path = e.Context.Request.RawUrl.TrimStart('/').Split('?')[0];
                Debug.WriteLine($"{path} -- {e.Context.Request.HttpMethod}");

                if (e.Context.Request.HttpMethod == "OPTIONS")
                {

                    Program.OutPutCode(e.Context, HttpStatusCode.NoContent);
                    return;
                }

                if (path.Length <= 1)
                {
                    // Here you can return a real html page for example
                    var indexFiles = new string[] { "index.html", "index.htm" };

                    foreach (var index in indexFiles)
                    {
                        var indexFileInfo = new FileInfo("I:/" + index);
                        if (indexFileInfo.Exists)
                        {
                            OutPutFile(e.Context,indexFileInfo);
                            return;
                        }
                    }

                    Program.OutPutString(e.Context, "<html><head>" +
                        "<title>Hi from nanoFramework Server</title></head><body>You want me to say hello in a real HTML page!<br/><a href='/useinternal'>Generate an internal text.txt file</a><br />" +
                        "<a href='/text.txt'>Download the Text.txt file</a><br>" +
                        "Try this url with parameters: <a href='/param.htm?param1=42&second=24&NAme=Ellerbach'>/param.htm?param1=42&second=24&NAme=Ellerbach</a></body></html>");
                    return;
                }
                if (path.ToLower() == "sayhello")
                {
                    // This is simple raw text returned
                    Program.OutPutString(e.Context, "It's working, url is empty, this is just raw text, /sayhello is just returning a raw text");
                    return;
                }
                if (path.ToLower() == "useinternal")
                {
                    // This tells the web server to use the internal storage and create a simple text file

                    using var testFile = new FileStream("I:/text.txt", FileMode.Create, FileAccess.ReadWrite);
                    byte[] buff = Encoding.UTF8.GetBytes("This is an example of file\r\nAnd this is the second line");
                    testFile.Write(buff, 0, buff.Length);
                    Program.OutPutString(e.Context, "Created a test file text.txt on internal storage");
                    return;
                }
                if (path.ToLower().IndexOf("param.htm") == 0)
                {
                    ParamHtml(e);
                    return;
                }
                if (path.ToLower().IndexOf("api/") == 0)
                {
                    // Check the routes and dispatch
                    var routes = path.TrimStart('/').Split('/');
                    if (routes.Length > 3)
                    {
                        var pinNumber = Convert.ToInt16(routes[2]);

                        // Do we have gpio ?
                        if (routes[1].ToLower() == "gpio")
                        {
                            if ((routes[3].ToLower() == "high") || (routes[3].ToLower() == "1"))
                            {
                                _controller.Write(pinNumber, PinValue.High);
                            }
                            else if ((routes[3].ToLower() == "low") || (routes[3].ToLower() == "0"))
                            {
                                _controller.Write(pinNumber, PinValue.Low);
                            }
                            else
                            {
                                Program.OutPutCode(e.Context, HttpStatusCode.BadRequest);
                                return;
                            }

                            Program.OutPutCode(e.Context, HttpStatusCode.OK);
                            return;
                        }
                        else if (routes[1].ToLower() == "open")
                        {
                            if (routes[3].ToLower() == "input")
                            {
                                if (!_controller.IsPinOpen(pinNumber))
                                {
                                    _controller.OpenPin(pinNumber);
                                }

                                _controller.SetPinMode(pinNumber, PinMode.Input);
                            }
                            else if (routes[3].ToLower() == "output")
                            {
                                if (!_controller.IsPinOpen(pinNumber))
                                {
                                    _controller.OpenPin(pinNumber);
                                }

                                _controller.SetPinMode(pinNumber, PinMode.Output);
                            }
                            else
                            {
                                Program.OutPutCode(e.Context, HttpStatusCode.BadRequest);
                                return;
                            }
                        }
                        else if (routes[1].ToLower() == "close")
                        {
                            if (_controller.IsPinOpen(pinNumber))
                            {
                                _controller.ClosePin(pinNumber);
                            }
                        }
                        else
                        {
                            Program.OutPutCode(e.Context, HttpStatusCode.BadRequest);
                            return;
                        }

                        Program.OutPutCode(e.Context, HttpStatusCode.OK);
                        return;
                    }
                    if (routes.Length == 2)
                    {
                        if (routes[1].ToLower() == "apikey")
                        {

                            if (e.Context.Request.HttpMethod != "POST")
                            {
                                Program.OutPutString(e.Context, apiKey);
                                return;
                            }

                            // Get the param from the body
                            if (e.Context.Request.ContentLength64 == 0)
                            {
                                Program.OutPutCode(e.Context, HttpStatusCode.BadRequest);
                                return;
                            }

                            byte[] buff = new byte[e.Context.Request.ContentLength64];
                            e.Context.Request.InputStream.Read(buff, 0, buff.Length);
                            string rawData = new string(Encoding.UTF8.GetChars(buff));
                            var parameters = rawData.Split('=');
                            if (parameters.Length < 2)
                            {
                                Program.OutPutCode(e.Context, HttpStatusCode.BadRequest);
                                return;
                            }

                            if (parameters[0].ToLower() == "newkey")
                            {
                                Program.OutPutCode(e.Context, HttpStatusCode.OK);
                                return;
                            }

                            Program.OutPutCode(e.Context, HttpStatusCode.BadRequest);
                            return;
                        }
                    }
                    else
                    {
                        ApiDefault(e);
                    }
                    return;
                }


                var fi = new FileInfo("I:/" + path);
                if (fi.Exists)
                {
                    Program.OutPutFile(e.Context, fi);
                    return;
                }
                Program.OutPutCode(e.Context, HttpStatusCode.NotFound);
                return;
            }
            catch (Exception ex)
            {
                Program.OutPutString(e.Context, $"错误：{ex.Message}");
            }
        }

        private static void ParamHtml(WebServerEventArgs e)
        {
            var url = e.Context.Request.RawUrl;
            // Test with parameters
            var parameters = WebServer.DecodeParam(url);
            string toOutput = "<html><head>" +
                "<title>Hi from nanoFramework Server</title></head><body>Here are the parameters of this URL: <br />";
            foreach (var par in parameters)
            {
                toOutput += $"Parameter name: {par.Name}, Value: {par.Value}<br />";
            }
            toOutput += "</body></html>";
            Program.OutPutString(e.Context, toOutput);
        }

        private static void ApiDefault(WebServerEventArgs e)
        {
            string ret = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=UTF-8\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n";
            ret += $"Your request type is: {e.Context.Request.HttpMethod}\r\n";
            ret += $"The request URL is: {e.Context.Request.RawUrl}\r\n";
            var parameters = WebServer.DecodeParam(e.Context.Request.RawUrl);
            if (parameters != null)
            {
                ret += "List of url parameters:\r\n";
                foreach (var param in parameters)
                {
                    ret += $"  Parameter name: {param.Name}, value: {param.Value}\r\n";
                }
            }

            if (e.Context.Request.Headers != null)
            {
                ret += $"Number of headers: {e.Context.Request.Headers.Count}\r\n";
            }
            else
            {
                ret += "There is no header in this request\r\n";
            }

            foreach (var head in e.Context.Request.Headers?.AllKeys)
            {
                ret += $"  Header name: {head}, Values:";
                var vals = e.Context.Request.Headers.GetValues(head);
                foreach (var val in vals)
                {
                    ret += $"{val} ";
                }

                ret += "\r\n";
            }

            if (e.Context.Request.ContentLength64 > 0)
            {

                ret += $"Size of content: {e.Context.Request.ContentLength64}\r\n";
                byte[] buff = new byte[e.Context.Request.ContentLength64];
                e.Context.Request.InputStream.Read(buff, 0, buff.Length);
                ret += $"Hex string representation:\r\n";
                for (int i = 0; i < buff.Length; i++)
                {
                    ret += buff[i].ToString("X") + " ";
                }

            }

            Program.OutPutString(e.Context, ret);

        }
    }
}

using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using System.Net.Http.Json;

namespace AliHelper
{
    internal class Helper
    {

        public static readonly Random RANDOM = new Random();
        public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true };

        public static readonly string versionName = "1.4.0";
        public static readonly string pubKey = "046BBE535F28BBDC4A3306AE15466C930CAD15644CF5F14BB7AC13C2FBC824144ACA7AEB7515E2E2F8AE6AA8DB0FFBB25AD0260ACA18E2DEFD7F93B5C0C38636E6";
        public static readonly string priKey = "00900523E0D13F081314039D0B3D93D7E297F27271ECC6F0727E0CA4C52E8CF031";
        public static readonly int woniuEncBytes = 93820200;
        public static readonly string SM4Key = "86760b05508b88b1e99995c819da019c";
        public static readonly string SM4Iv = "945f01afa14f7a68943f97117496e573";
        public static readonly string AppId = "25dzX3vbYqktVxyX";
        public static readonly int ExpireSecond = 115200;
        public static readonly string wonuiUrl = "https://snail8.cn";
        public static readonly string aliOpenUrl = "https://openapi.aliyundrive.com";
        public static readonly string aliWebUrl = "https://api.aliyundrive.com";
        public static readonly string aliWebLoginUrl = "https://passport.aliyundrive.com";

        private static readonly SM2Engine sm2Encoder = new(new SM3Digest(), SM2Engine.Mode.C1C3C2);
        private static readonly SM2Engine sm2Decoder = new(new SM3Digest(), SM2Engine.Mode.C1C3C2);
        private static readonly char[] hexChars = [.. "0123456789ABCDEF"];
        private static readonly Dictionary<char, byte> hexDict = "0123456789ABCDEF".Select((c, i) => (c, i: (byte)i))
            .Concat("abcdef".Select((c, i) => (c, i: (byte)(i + 10))))
            .ToDictionary(s => s.c, s => s.i);
        private static readonly HttpClient client = new();
        static Helper()
        {
            var x9 = GMNamedCurves.GetByName("SM2P256V1");

            ICipherParameters ePara = new ParametersWithRandom(new ECPublicKeyParameters(x9.Curve.DecodePoint(FromHex(pubKey)), new ECDomainParameters(x9)));

            //byte[]? userId = null;
            //if (userId != null)
            //{
            //    ePara = new ParametersWithID(ePara, userId);
            //}

            sm2Encoder.Init(true, ePara);

            ICipherParameters dPara = new ParametersWithRandom(new ECPrivateKeyParameters(new(FromHex(priKey)), new(x9)));

            //if (userId != null)
            //{
            //    dPara = new ParametersWithID(dPara, userId);
            //}

            sm2Decoder.Init(false, dPara);
        }
        static byte[] FromHex(string hex)
        {
            var result = GC.AllocateUninitializedArray<byte>(hex.Length / 2);
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = (byte)((hexDict[hex[i * 2]] << 4) | hexDict[hex[i * 2 + 1]]);
            }
            return result;
        }

        static string GetSign<T>(T obj)
        {
            var str = JsonSerializer.Serialize(obj);
            var data = Encoding.UTF8.GetBytes(str);
            var sign = sm2Encoder.ProcessBlock(data, 0, data.Length);
            var result = new char[sign.Length * 2];
            for (var i = 0; i < sign.Length; i++)
            {
                result[i * 2] = hexChars[sign[i] >> 4];
                result[i * 2 + 1] = hexChars[sign[i] & 0xf];
            }
            return new string(result);
        }

        public static async Task<JsonElement?> WoniuQueryAsync<T>(string url, T obj, Dictionary<string, string?>? paras = null, CancellationToken token = default)
        {
            var sign = GetSign(obj);
            var fullUrl = new StringBuilder(wonuiUrl, 240);
            fullUrl.Append(url);
            fullUrl.Append("?sign=");
            fullUrl.Append(sign);
            if (paras != null && paras.Count > 0)
            {
                foreach (var para in paras)
                {
                    fullUrl.Append('&');
                    fullUrl.Append(para.Key);
                    fullUrl.Append('=');
                    fullUrl.Append(Uri.EscapeDataString(para.Value??""));
                }
            }
            using var rep = await client.PostAsync(fullUrl.ToString(), null, token);
            var repStr = await rep.Content.ReadAsStringAsync(token);
            var repBytes = FromHex(repStr);
            var dresult = sm2Decoder.ProcessBlock(repBytes, 0, repBytes.Length);
            return JsonSerializer.Deserialize<JsonElement>(dresult);
        }
        public static async Task<JsonElement?> GetAliWebQrCodeAsync(CancellationToken token = default)
        {
            return await client.GetFromJsonAsync<JsonElement>($"{aliWebLoginUrl}/newlogin/qrcode/generate.do?appName=aliyun_drive&fromSite=52&appName=aliyun_drive&appEntrance=web_default&_csrf_token=8iPG8rL8zndjoUQhrQnko5&umidToken=27f197668ac305a0a521e32152af7bafdb0ebc6c&isMobile=false&lang=zh_CN&returnUrl=&hsiz=1d3d27ee188453669e48ee140ea0d8e1&fromSite=52&bizParams=&_bx-v=2.5.3", token);
        }

        public static async Task<JsonElement?> CheckAliOpenQrcodeAsync(string sid, CancellationToken token = default)
        {
            return await client.GetFromJsonAsync<JsonElement>($"{aliOpenUrl}/oauth/qrcode/{sid}/status", token);
        }
        public static async Task<JsonElement?> CheckAliWebQrcodeAsync(string ck, string t, CancellationToken token = default)
        {
            var content = new StringContent($"ck={Uri.EscapeDataString(ck)}&t={Uri.EscapeDataString(t)}");
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            await content.LoadIntoBufferAsync();
            using var rep = await client.PostAsync($"{aliWebLoginUrl}/newlogin/qrcode/query.do?appName=aliyun_drive&fromSite=52&_bx-v=2.0.31", content, token);
            var repStr = await rep.Content.ReadAsStringAsync(token);
            return JsonSerializer.Deserialize<JsonElement>(repStr);
        }

        public static async Task<JsonElement?> AliOpenQueryAsync(string url, string? access_token, Dictionary<string, object?>? paras = null, CancellationToken token = default)
        {
            var fullUrl = $"{aliOpenUrl}{url}";
            var message = new HttpRequestMessage(HttpMethod.Post, fullUrl.ToString());
            if (!string.IsNullOrWhiteSpace(access_token))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access_token);
            }
            message.Content = JsonContent.Create(paras);
            await message.Content.LoadIntoBufferAsync();
            using var rep = await client.SendAsync(message, token);
            var repStr = await rep.Content.ReadAsStringAsync(token);
            return JsonSerializer.Deserialize<JsonElement>(repStr);
        }
    }
}

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

byte[] aeskey = [0x52,0x36,0x46,0x1A,0xD3,0x85,0x03,0x66,
                0x90,0x45,0x16,0x28,0x79,0x03,0x36,0x23,
                0xDD,0xBE,0x6F,0x03,0xFF,0x04,0xE3,0xCA,
                0xD5,0x7F,0xFC,0xA3,0x50,0xE4,0x9E,0xD9];
byte[] iv = [0xE0, 0x7A, 0xAD, 0x35, 0xE0, 0x90, 0xAA, 0x03, 0x8A, 0x51, 0xFD, 0x05, 0xDF, 0x8C, 0x5D, 0x0F];

//SSF = Skin + AES(Zlib)
//Zlib = Len(Con)+Zlib(Con)
//Con = Len(Con)+OffsetFiles+Files
//OffsetFiles = o1+o2+o3+...;
//Files = f1+f2+f3+...;
//File = namelen + name + contentlen + content;
var bytes = File.ReadAllBytes(args[0]);
var aes = Aes.Create();
aes.Key = aeskey;
aes.IV = iv;
aes.Mode = CipherMode.CBC;
var bytes2 = aes.DecryptCbc(bytes[8..], iv);
//var size = BitConverter.ToInt32(bytes2, 0);
using var stream = new ZLibStream(new MemoryStream(bytes2[4..]), CompressionMode.Decompress);
using var ms = new MemoryStream();
stream.CopyTo(ms);
var buffer = ms.ToArray();
//size = BitConverter.ToInt32(buffer, 0);
var offsetSize = BitConverter.ToUInt32(buffer, 4);
var di = new DirectoryInfo(args[1]);
if (!di.Exists)
{
    di.Create();
}
for (var i = 0; i < offsetSize; i += 4)
{
    var offset = BitConverter.ToInt32(buffer, i + 8);
    var nameLen = BitConverter.ToInt32(buffer, offset);
    var name = Encoding.Unicode.GetString(buffer, offset + 4, nameLen);
    var clen = BitConverter.ToInt32(buffer, offset + 4 + nameLen);
    var content = buffer[(offset + 8 + nameLen)..(offset + 8 + nameLen + clen)];
    File.WriteAllBytes(Path.Combine(di.FullName, name), content);
    Console.WriteLine($"{name} - {clen}");
}
Console.WriteLine("ok");
Console.ReadLine();
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Text;

namespace muludll
{
    public class Main
    {
        private static MemoryMappedFile mmf;
        public static int Inject(string _)
        {
            var ass = Assembly.GetEntryAssembly();
            var types = ass.GetTypes();
            var strType = typeof(string);
            var passList = new List<int>();
            foreach (var type in types)
            {
                foreach (var f in type.GetFields(BindingFlags.Static | BindingFlags.Public))
                {
                    if (f.FieldType == strType)
                    {
                        var v = f.GetValue(null) as string;
                        if (v != null && v.Length == 6 && int.TryParse(v, out var pass))
                        {
                            passList.Add(pass);
                        }
                    }
                }
            }
            if (passList.Count > 0)
            {
                var text = "尝试以下激活码：" + Environment.NewLine + string.Join(Environment.NewLine, passList);
                var bytes = Encoding.UTF8.GetBytes(text);
                bytes = BitConverter.GetBytes(bytes.Length).Concat(bytes).ToArray();
                mmf = MemoryMappedFile.CreateOrOpen($"{Process.GetCurrentProcess().Id}.pass", bytes.Length);
                using (var s = mmf.CreateViewStream())
                {
                    s.Write(bytes, 0, bytes.Length);
                }
            }
            return passList.Count;
        }
    }
}

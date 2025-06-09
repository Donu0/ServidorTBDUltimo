using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace ServidorTBD
{
    public class Config
    {
        public string IP { get; set; }
        public int Port { get; set; }

        public static Config Load(string path)
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<Config>(json);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FileSync
{
    internal abstract class Settings
    {
        private readonly string _path;
        public Settings(string path)
        {
            _path = path;
            JsonConvert.PopulateObject(File.ReadAllText(path), this);
        }
        public void Save(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this));
        }
        public void Save()
        {
            Save(_path);
        }
    }
}

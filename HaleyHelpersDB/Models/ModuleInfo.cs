using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haley.Utils;

namespace Haley.Models{
    //Contains information needed for the module to load it's queries.
    public class ModuleInfo<M> where M : IDBModule {
        public string Key { get; set; }
        public Dictionary<string, object> Seed { get; set; }
        public M Module { get; set; }

        public ModuleInfo(string key, M module, Dictionary<string, object> seed) {
            Key = key;
            Module = module;
            Seed = seed ?? new Dictionary<string, object>();
        }

        public ModuleInfo(string key, M module) : this (key,module,null){
        }
        public ModuleInfo(Enum @enum, M module) : this(@enum.GetKey(), module, null) {
        }
        public ModuleInfo(Enum @enum, M module, Dictionary<string, object> seed) :this (@enum.GetKey(),module,seed){
        }
    }
}
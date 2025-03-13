using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models
{
    public abstract class ModuleParameter : ParameterBase{
        internal IDBAdapter Adapter { get; set; }
        public ModuleParameter(): this (null) { }
        public ModuleParameter(string key):base(key) { }
    }
}

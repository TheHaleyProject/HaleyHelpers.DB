using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models
{
    public abstract class ModuleParameter : IModuleParameter {
        public string AdapterKey { get; set; } 
        public Dictionary<string, string> QueryParams { get; protected set; }
        public ModuleParameter(): this (null) { } 
        public ModuleParameter(string adapterKey) {
            AdapterKey = adapterKey;
            QueryParams = new Dictionary<string, string>(); } 
    }
}

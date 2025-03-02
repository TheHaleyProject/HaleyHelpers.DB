using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models
{
    public abstract class ModuleParameter : IModuleParameter {
        public Dictionary<string, string> QueryParams { get; protected set; } 

        public ModuleParameter() { QueryParams = new Dictionary<string, string>(); }
    }
}

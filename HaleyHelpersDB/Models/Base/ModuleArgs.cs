using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models
{
    public class ModuleArgs : ParameterBase, IModuleArgs{
        internal IDBAdapter Adapter { get; set; }
        public object[] Arguments { get; protected set; }
        public void ClearParameters() => ClearParametersInternal(true);
        public void ClearParameters(string groupKey) => ClearParametersInternal(groupKey);
        public void UpsertParameter(string key, object value, bool replace = true) => AddParameterInternal(key, value, replace);
        public void UpsertParameter(string groupKey, string key, object value, bool replace = true) => AddParameterInternal(groupKey, key, value, replace);
        public void SetParameters(Dictionary<string, object> parameters) => SetParametersInternal(parameters);
        public void SetParameters(string groupKey, Dictionary<string, object> parameters) => SetParametersInternal(groupKey, parameters);
        public bool TransactionMode { get; set; }
        public static void Assert(params object[] input) {
            if (input.Any(p => p == null)) throw new ArgumentNullException("Required input object is missing");
        }
        public ModuleArgs(string key) : base(key) { }
        public ModuleArgs() { }
    }
}

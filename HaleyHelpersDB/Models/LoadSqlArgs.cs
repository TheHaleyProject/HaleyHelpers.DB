using Haley.Abstractions;
using Haley.Enums;
using System.Globalization;

namespace Haley.Models
{
	public class LoadSqlArgs {
        public string Key { get; set; }
        public string FallBackDBName { get; set; }
        internal string DBName { get; set; }
        public string SQLPath { get; set; }
        public Dictionary<string, string> VariablesToReplace { get; set; } = new Dictionary<string, string>();
        public TargetDB TargetDB { get; set; } = TargetDB.maria;
        public LoadSqlArgs(string adapter_key) { Key = adapter_key;
        }
	}
}

using Haley.Abstractions;
using Haley.Enums;
using System.Globalization;

namespace Haley.Models
{
	public class CreateSchemaArgs {
        public string Key { get; set; }
        public string FallBackDBName { get; set; }
        internal string DBName { get; set; }
        public string SQLPath { get; set; }
        public string SQLContent { get; set; }
        public Dictionary<string, string> VariablesToReplace { get; set; } = new Dictionary<string, string>();
        public TargetDB TargetDB { get; set; } = TargetDB.maria;
        public CreateSchemaArgs(string adapter_key) { Key = adapter_key;
        }
	}
}

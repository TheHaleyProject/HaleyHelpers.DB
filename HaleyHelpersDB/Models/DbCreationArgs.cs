using Haley.Abstractions;
using Haley.Enums;
using System.Globalization;

namespace Haley.Models
{
	public class DbCreationArgs {
        public string Key { get; set; }
        public string FallBackDBName { get; set; }
        internal string DBName { get; set; }
        public string SQLPath { get; set; }
        public string SQLContent { get; set; }
        public Dictionary<string, string> VariablesToReplace { get; set; } = new Dictionary<string, string>();
        public Func<string ,string,string> ContentProcessor { get; set; }
        public DbCreationArgs(string adapter_key) { Key = adapter_key;
        }
	}
}

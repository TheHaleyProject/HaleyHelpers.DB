using Haley.Abstractions;
using Haley.Enums;
using Microsoft.Extensions.Logging;

namespace Haley.Models {
    public class DBSInput : IDBInput {
        public string DBAKey { get; set; }
        public ResultFilter Filter { get; set; }
        public string Conn { get; set; }
        public string Query { get; set; }
        public ILogger  Logger { get; set; }
        internal bool ReturnsResult { get; set; }
        public Func<string, object, bool> ParamHandler { get; set; }
        public string OutputName { get; set; }
        public bool Prepare { get; set; } = false;
        public DBSInput(string dba_key) {
            DBAKey = dba_key;
            Filter = ResultFilter.None; }
    }
}

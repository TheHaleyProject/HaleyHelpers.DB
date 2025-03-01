using Haley.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {
    public class DBSInput {
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

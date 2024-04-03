using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {
    public class DBAResult {
        public string Message { get; set; }
        public override string ToString() {
            return Message ?? "DBA Result";
        }
        public DBAResult(string message) { Message = message; }
    }
}

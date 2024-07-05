using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {
    public abstract class DBAResult {
        protected string _message;
        public override string ToString() {
            return _message ?? "DBAResult";
        }
        public DBAResult(string message) { _message = message; }
    }
}

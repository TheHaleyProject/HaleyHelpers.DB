using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {
    public class DBAError : DBAResult {
        public DBAError(string message) : base(message) { }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {
    public class DBAError {
        public string Error { get; set; }
        public override string ToString() {
            return Error ?? "DBA Error";
        }
        public DBAError(string message) { Error = message; }
    }
}

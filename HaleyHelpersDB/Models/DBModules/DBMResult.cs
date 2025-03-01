using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models
{
    public class DBMResult
    {
        public bool Status { get; set; }
        public string Message { get; set; }
        public object Result { get; set; }
        public DBMResult(bool status, string message, object result) {
            Status = status;
            Message = message;
            Result = result;
        }
        public DBMResult(bool status) : this(status, null, null) { }
        public DBMResult(bool status,string message) : this(status, message, null) { }
    }
}

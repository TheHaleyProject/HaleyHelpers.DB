using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class CMDAttribute : Attribute {
        public object Name { get; set; }

        public CMDAttribute(object name) {
            Name = name;
        }
    }
}
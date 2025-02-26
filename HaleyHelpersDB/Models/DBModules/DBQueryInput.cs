using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models{
   //Reason for creating this as struct is that we don't need to change the referenced value anywhere.
    public class DBQueryInput {
        //internal string DBAKey { get; set; }
        //public string InputJson { get; set; }
        //public ModuleParam(string dbakey) {
        //    if (string.IsNullOrWhiteSpace(dbakey)) throw new ArgumentException("DBA Key cannot be null.");
        //    DBAKey = dbakey; }

        //protected bool ArgumentsPresent(out object error, params object[] args) {
        //    error = null;
        //    if (args.Count() > 0) {
        //        foreach (var item in args) {
        //            if (item == null) {
        //                error = _dbservice?.GetFirst(new DBAError($@"{item.GetType().Name}"))?.Result;
        //                return false;
        //            }
        //        }
        //    }
        //    return true;
        //}
        public DBQueryInput() { }
    }
}
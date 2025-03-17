using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models
{
    public abstract class DBModuleInput : ParameterBase, IDBModuleInput{
        internal IDBAdapter Adapter { get; set; }
        public Enum Command { get; protected set; }
        public void UpdateCommand(Enum command) {
            Command = command; //Change the command;
        }
        public void ClearParameters() => ClearParametersInternal();
        public object[] Arguments { get; protected set; }
        public bool TransactionModeOnly { get; set; }

        public void Assert(params object[] input) {
            if (input.Any(p => p == null)) throw new ArgumentNullException("Required input object is missing");
        }

        public DBModuleInput(Enum cmd,string key) : base(key) { Command = cmd; }
    }
}

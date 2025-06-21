using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models
{
    public abstract class ModuleArgs : ParameterBase, IModuleArgs{
        protected Dictionary<Enum, Dictionary<string, object>> _groupParameters = new Dictionary<Enum, Dictionary<string, object>>(); //Group parameters
        protected Dictionary<Enum, object[]> _groupArguments = new Dictionary<Enum, object[]>();
        internal IDBAdapter Adapter { get; set; }
        public Enum Command { get; protected set; }
        public object[] Arguments { get; protected set; }

        public void UpdateCommand(Enum command) {
            Command = command; //Change the command;
        }

        protected void AddGroupParameter(Enum groupKey, string key, object value, bool replace = true) {
            if (string.IsNullOrWhiteSpace(key) || groupKey == null) throw new ArgumentNullException($@"Group parameter add failed. GroupKey & Key are mandatory");
            if (!_groupParameters.ContainsKey(groupKey)) _groupParameters.Add(groupKey, new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase));
            if (_groupParameters[groupKey].ContainsKey(key)) {
                if (!replace) return;//Contains the key and replace is also not allowed.
                _groupParameters[groupKey][key] = value;
            } else {
                //return _parameters.TryAdd(key, value); //For concurrency. At the moment, lets focus only on direct dictionaries
                _groupParameters[groupKey].Add(key, value);
            }
        }

        protected void SetParameters(Enum groupKey) {
            ClearParameters();
            if (!_groupParameters.ContainsKey(groupKey)) return;
            SetParametersInternal(_groupParameters[groupKey]);
        }

        protected void SetArguments(Enum groupKey) {
            ClearArguments();
            if (!_groupArguments.ContainsKey(groupKey)) return;
            Arguments = _groupArguments[groupKey];
        }

        protected void AddGroupArgument(Enum groupKey, params object[] args) {
            if (groupKey == null) throw new ArgumentNullException($@"Group Argument add failed. GroupKey is mandatory");
            if (!_groupArguments.ContainsKey(groupKey)) {
                _groupArguments[groupKey] = args;
            } else {
                //return _parameters.TryAdd(key, value); //For concurrency. At the moment, lets focus only on direct dictionaries
                _groupArguments.Add(groupKey, args);
            }
        }

        public void ClearParameters() {
            ClearParametersInternal();
        }

        public void ClearArguments() {
            Arguments = new object[] { };
        }

        public void ClearGroupParameters(Enum groupKey) {
            if (groupKey == null) return;
            if (!_groupParameters.ContainsKey(groupKey)) return;
            _groupParameters[groupKey].Clear();
        }

        public bool TransactionMode { get; set; }
        public void Assert(params object[] input) {
            if (input.Any(p => p == null)) throw new ArgumentNullException("Required input object is missing");
        }

        public ModuleArgs(Enum cmd,string key) : base(key) { Command = cmd; }
    }
}

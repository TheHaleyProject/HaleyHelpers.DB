using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models
{
    public abstract class DBModuleInput : ParameterBase, IDBModuleInput{
        protected Dictionary<string, Dictionary<string, object>> _groupParameters = new Dictionary<string, Dictionary<string, object>>(StringComparer.InvariantCultureIgnoreCase); //Group parameters
        protected Dictionary<string, object[]> _groupArguments = new Dictionary<string, object[]>(StringComparer.InvariantCultureIgnoreCase);
        internal IDBAdapter Adapter { get; set; }
        public Enum Command { get; protected set; }
        public object[] Arguments { get; protected set; }

        public void UpdateCommand(Enum command) {
            Command = command; //Change the command;
        }

        protected void AddGroupParameter(string groupKey, string key, object value, bool replace = true) {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(groupKey)) throw new ArgumentNullException($@"Group parameter add failed. GroupKey & Key are mandatory");
            if (!_groupParameters.ContainsKey(groupKey)) _groupParameters.Add(groupKey, new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase));
            if (_groupParameters[groupKey].ContainsKey(key)) {
                if (!replace) return;//Contains the key and replace is also not allowed.
                _groupParameters[groupKey][key] = value;
            } else {
                //return _parameters.TryAdd(key, value); //For concurrency. At the moment, lets focus only on direct dictionaries
                _groupParameters[groupKey].Add(key, value);
            }
        }

        protected void SetParameters(string groupKey) {
            ClearParameters();
            if (!_groupParameters.ContainsKey(groupKey)) return;
            SetParametersInternal(_groupParameters[groupKey]);
        }

        protected void SetArguments(string groupKey) {
            ClearArguments();
            if (!_groupArguments.ContainsKey(groupKey)) return;
            Arguments = _groupArguments[groupKey];
        }

        protected void AddGroupArgument(string groupKey, params object[] args) {
            if (string.IsNullOrWhiteSpace(groupKey)) throw new ArgumentNullException($@"Group Argument add failed. GroupKey is mandatory");
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

        public void ClearGroupParameters(string groupKey) {
            if (string.IsNullOrWhiteSpace(groupKey)) return;
            if (!_groupParameters.ContainsKey(groupKey)) return;
            _groupParameters[groupKey].Clear();
        }

        public bool TransactionMode { get; set; }
        public void Assert(params object[] input) {
            if (input.Any(p => p == null)) throw new ArgumentNullException("Required input object is missing");
        }

        public DBModuleInput(Enum cmd,string key) : base(key) { Command = cmd; }
    }
}

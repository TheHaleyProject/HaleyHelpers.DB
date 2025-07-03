using Haley.Abstractions;
using Haley.Enums;
using Microsoft.Extensions.Logging;

namespace Haley.Models {
    public class AdapterArgs : ParameterBase, IAdapterArgs {
        internal IDBAdapter Adapter { get; set; }
       
        public ResultFilter Filter { get; set; }
        public string Query { get; set; }
        public ILogger  Logger { get; set; }
        internal bool ReturnsResult { get; set; }
        internal bool IsScalar { get; set; }
        public Func<string, object, bool> ParamHandler { get; set; }
        public string OutputName { get; set; }
        public bool Prepare { get; set; } = false;
        public bool TransactionMode { get; set; }
        public IAdapterArgs SetAdapterKey(string key) {
            Key = key;
            return this;
        }

        public IAdapterArgs SetParameters(Dictionary<string, object> parameters) {
             SetParametersInternal(parameters);
            return this;
        }

        public IAdapterArgs UpsertParameter(string key, object value, bool replace = true) {
            AddParameterInternal(key, value, replace);
            return this;
        }
        public IAdapterArgs UpsertParameter(string groupKey, string key, object value, bool replace = true) {
            AddParameterInternal(groupKey, key, value, replace);
            return this;
        }
        public IAdapterArgs SetParameters(string groupKey, Dictionary<string, object> parameters) {
            SetParametersInternal(groupKey, parameters);
            return this;
        }
        public AdapterArgs(string key) :base (key){
            Filter = ResultFilter.None; }
    }
}

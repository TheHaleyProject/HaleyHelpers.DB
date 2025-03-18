using Haley.Abstractions;
using Haley.Enums;
using Microsoft.Extensions.Logging;

namespace Haley.Models {
    public class AdapterParameter : ParameterBase, IAdapterParameter {
        internal IDBAdapter Adapter { get; set; }
        internal void SetParameters(Dictionary<string, object> parameters) => SetParametersInternal(parameters);
        public ResultFilter Filter { get; set; }
        public string Query { get; set; }
        public ILogger  Logger { get; set; }
        internal bool ReturnsResult { get; set; }
        internal bool IsScalar { get; set; }
        public Func<string, object, bool> ParamHandler { get; set; }
        public string OutputName { get; set; }
        public bool Prepare { get; set; } = false;
        public bool TransactionMode { get; set; }
        public AdapterParameter(string key) :base (key){
            Filter = ResultFilter.None; }
    }
}

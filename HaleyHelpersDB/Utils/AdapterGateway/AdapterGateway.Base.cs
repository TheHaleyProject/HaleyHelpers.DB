using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;

namespace Haley.Utils {

    public partial class AdapterGateway  {
        public string GetSchemaName(string adapterKey) {
            if (!this.ContainsKey(adapterKey)) return null;
            return this[adapterKey].Info.SchemaName;
        }

        public void SetDefaultAdapterKey(string adapterKey) {
            //Set the adapter key for all the modules (if not provided along with the parameter)
            if (string.IsNullOrWhiteSpace(adapterKey)) throw new ArgumentNullException(nameof(adapterKey));
            _defaultAdapterKey = adapterKey;
        }
        public IAdapterGateway UpdateAdapter() {
            Configure(true);
            Updated?.Invoke();
            return this;
        }

        IDBAdapter GetAdapter(IAdapterArgs input) {
            if (input is AdapterArgs adp && adp.Adapter != null) return adp.Adapter;
            if (string.IsNullOrWhiteSpace(input.Key)) throw new ArgumentException("Adapter key cannot be empty");
            if (!ContainsKey(input.Key)) throw new ArgumentNullException($@"DBAKey missing: {input.Key} is not found in the dictionary");
            return this[input.Key];
        }

        protected virtual bool TryGetDefaultKey(out string key) {
            key = _defaultAdapterKey ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)) return false;
            return true;
        }

        protected IAdapterConfig GetAdapterInfo(string adapterKey) {
            if (string.IsNullOrWhiteSpace(adapterKey) || !ContainsKey(adapterKey)) throw new ArgumentNullException($@"Adapter key not registered {adapterKey}");
            return this[adapterKey].Info.Clone() as IAdapterConfig; //All connection strings properly parsed.
        }

        public void SetServiceUtil(IGatewayUtil util) {
            _util = util;
        }

        public IFeedback DuplicateAdapter(string existingAdapterKey, string newAdapterKey, params (string key, string value)[] connectionStringReplacements) {
            existingAdapterKey.AssertValue(true);
            newAdapterKey.AssertValue(true);
            var result = new Feedback(false);
            if (ContainsKey(newAdapterKey)) return result.SetMessage($@"Adapter with key {newAdapterKey} is already registered"); //Already exists.
            if (!ContainsKey(existingAdapterKey)) result.SetMessage($@"No adapter is registered for the key {existingAdapterKey}"); //Already exists.
            var existing = this[existingAdapterKey];
            var infoClone = (IAdapterConfig)existing.Info.Clone();
            var newConStr = infoClone.ConnectionString.ReplaceValues(';', connectionStringReplacements);
            infoClone.AdapterKey = newAdapterKey;
            infoClone.ConnectionString = newConStr;
            infoClone.DBName = ParseConnectionString(newConStr, DBNAME_KEY);
            Add(infoClone, true);
            return result.SetStatus(true);
        }
    }
}
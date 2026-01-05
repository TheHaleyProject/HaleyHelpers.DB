using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;

namespace Haley.Utils {

    public partial class AdapterGateway {

        public IAdapterGateway Add(IAdapterConfig entry, bool replace = true) {
            var adapter = new DBAdapter(entry);

            if (!replace && ContainsKey(entry.AdapterKey)) return this;

            if (replace && ContainsKey(entry.AdapterKey)) {
                //remove the adapter
                if (!TryRemove(entry.AdapterKey, out _)) {
                    throw new ArgumentException($@"Key {entry.AdapterKey} already exists and unable to replace it as well.");
                }; //remove the item.
            }

            if (TryAdd(entry.AdapterKey, adapter)) {
                return this; //Trying to add the key and adapter here
            }
            throw new ArgumentException("Unable to add DBAdapter to dictionary.");
        }

        #region Execution

        public ITransactionHandler GetTransactionHandler(string adapterKey) {
            return new TransactionHandler(GetAdapterInfo(adapterKey)) {_dbs = GetDBService() }; 
        }

        public ITransactionHandler GetTransactionHandler() {
            if (!TryGetDefaultKey(out var key)) throw new ArgumentException("Adapter key cannot be empty, while creating the transaction.");
            return GetTransactionHandler(key);
        }

        public Task<object> Read(IParameterBase input, string query, params (string key, object value)[] parameters) {
            return Read(input.ToAdapterArgs(query), parameters);
        }

        public Task<object> Scalar(IParameterBase input, string query, params (string key, object value)[] parameters) {
            return Scalar(input.ToAdapterArgs(query), parameters);
        }

        public Task<object> NonQuery(IParameterBase input, string query, params (string key, object value)[] parameters) {
            return NonQuery(input.ToAdapterArgs(query), parameters);
        }

        public Task<object> Read(IAdapterArgs input,  params (string key, object value)[] parameters) => ExecuteInternal(input, (inp) => { return GetAdapter(inp).Read(inp, parameters); });

        public Task<object> Scalar(IAdapterArgs input, params (string key, object value)[] parameters) => ExecuteInternal(input, (inp) => { return GetAdapter(inp).Scalar(inp, parameters); });

        public Task<object> NonQuery(IAdapterArgs input, params (string key, object value)[] parameters) => ExecuteInternal(input, (inp) => { return GetAdapter(inp).NonQuery(inp, parameters); });

        async Task<object> ExecuteInternal(IAdapterArgs input, Func<IAdapterArgs,Task<object>> executor) {
            try {
                object result = null;
                input.LogQueryInConsole = LogQueryInConsole; //set the logging preference
                var inputEx = input as AdapterArgs;
                if (inputEx != null && string.IsNullOrWhiteSpace(inputEx.Key) && TryGetDefaultKey(out var _key)) {
                    input.SetAdapterKey(_key);
                }

                result = await executor.Invoke(input);
                if (_util != null) return _util.Convert(result); //we know that it is not a dictionary
                return result;
            } catch (Exception ex) {
                input.Logger?.LogError($@"Error for: {input.Query}");
                input.Logger?.LogError(ex.Message);
                input.Logger?.LogError(ex.StackTrace);
                if (ThrowCRUDExceptions) throw;
                return new FeedbackError(ex.Message);
            }
        }
        #endregion
    }
}
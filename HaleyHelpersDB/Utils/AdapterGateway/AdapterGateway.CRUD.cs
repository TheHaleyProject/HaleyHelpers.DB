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

        public async Task<object> Read(IAdapterArgs input,  params (string key, object value)[] parameters) {
            if (input is AdapterArgs inputEx) inputEx.ReturnsResult = true;
            return await ExecuteInternal(input, parameters);
        }

        public async Task<object> Scalar(IAdapterArgs input, params (string key, object value)[] parameters) {
            if (input is AdapterArgs inputEx) {
                inputEx.ReturnsResult = true;
                inputEx.IsScalar = true;
            }
            return await ExecuteInternal(input, parameters);
        }

        public async Task<object> NonQuery(IAdapterArgs input, params (string key, object value)[] parameters) {
            if (input is AdapterArgs inputEx) inputEx.ReturnsResult = false;
            return await ExecuteInternal(input, parameters);
        }

        public async Task<object> GetFirst(object input, ResultFilter filter = ResultFilter.None) {
            //Now, apply internal methods to get the result
            input = input.ApplyFilter(filter);
            if (_util != null) return _util.Convert(input); //we know that it is not a dictionary
            return input;
        }

        //(string key, object value, ParameterDirection direction)[] ParamAdapter(params (string key, object value)[] parameters) {
        //    List<(string key, object value, ParameterDirection direction)> result = new List<(string key, object value, ParameterDirection direction)>();
        //    foreach (var item in parameters) {
        //        result.Add((item.key, item.value, ParameterDirection.Input));
        //    }
        //    return result.ToArray();
        //}

        async Task<object> ExecuteInternal(IAdapterArgs input, params (string key, object value)[] parameters) {
            try {
                object result = null;
                var inputEx = input as AdapterArgs;
                if (inputEx != null && string.IsNullOrWhiteSpace(inputEx.Key) && TryGetDefaultKey(out var _key)) {
                    input.SetAdapterKey(_key);
                }

                if (inputEx != null && inputEx.ReturnsResult) {
                    if (inputEx.IsScalar) {
                        result = (await GetAdapter(input).Scalar(input, parameters));
                    } else {
                        result = ((DataSet)await GetAdapter(input).Read(input, parameters))?.Select(true)?.Convert(input.JsonStringAsNode)?.ToList();
                    }
                } else {
                    result = await GetAdapter(input).NonQuery(input, parameters);
                }
                return await GetFirst(result,input.Filter);
            } catch (Exception ex) {
                input.Logger?.LogError($@"Error for: {input.Query}");
                input.Logger?.LogError(ex.Message);
                input.Logger?.LogError(ex.StackTrace);
                if (ThrowCRUDExceptions) throw ex;
                return await GetFirst(new FeedbackError(ex.Message));
            }
        }
        #endregion
    }
}
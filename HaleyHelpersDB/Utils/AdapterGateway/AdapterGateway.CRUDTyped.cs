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

        public Task<IFeedback<DbRows>> ReadAsync(string key, string query, params (string key, object value)[] parameters) => ReadAsync(new AdapterArgs(key) { Query = query }, parameters);
        public Task<IFeedback<DbRow>> ReadSingleAsync(string key, string query, params (string key, object value)[] parameters) => ReadSingleAsync(new AdapterArgs(key) { Query = query, Filter = ResultFilter.FirstDictionary }, parameters);
        public Task<IFeedback<T>> ScalarAsync<T>(string key, string query, params (string key, object value)[] parameters) => ScalarAsync<T>(new AdapterArgs(key) { Query = query }, parameters);
        public Task<IFeedback<int>> NonQueryAsync(string key, string query, params (string key, object value)[] parameters) => NonQueryAsync(new AdapterArgs(key) { Query = query }, parameters);

        public Task<IFeedback<DbRows>> ReadAsync(IAdapterArgs input, params (string key, object value)[] parameters) => SafeInternalExecute((inp) => GetAdapter(input).ReadAsync(input, parameters),input);

        public Task<IFeedback<DbRow>> ReadSingleAsync(IAdapterArgs input, params (string key, object value)[] parameters) => SafeInternalExecute((inp) => GetAdapter(input).ReadSingleAsync(input, parameters), input);

        public Task<IFeedback<T>> ScalarAsync<T>(IAdapterArgs input, params (string key, object value)[] parameters) => SafeInternalExecute((inp) => GetAdapter(input).ScalarAsync<T>(input, parameters), input);

        public Task<IFeedback<int>> NonQueryAsync(IAdapterArgs input, params (string key, object value)[] parameters) => SafeInternalExecute((inp) => GetAdapter(input).NonQueryAsync(input, parameters), input);

        Task<IFeedback<T>> SafeInternalExecute<T>(Func<IAdapterArgs, Task<IFeedback<T>>> executor, IAdapterArgs input) {
            try {
                input.LogQueryInConsole = LogQueryInConsole; //set the logging preference
                var inputEx = input as AdapterArgs;
                if (inputEx != null && string.IsNullOrWhiteSpace(inputEx.Key) && TryGetDefaultKey(out var _key)) {
                    input.SetAdapterKey(_key);
                }
                return executor.Invoke(input);
            } catch (Exception ex) {
                input.Logger?.LogError($@"Error for: {input.Query}");
                input.Logger?.LogError(ex.Message);
                input.Logger?.LogError(ex.StackTrace);
                if (ThrowCRUDExceptions) throw ex;
                var fb = new Feedback<T>();
                return Task.FromResult(fb.SetStatus(false).SetMessage(ex.Message)); // adapt to your API
            }
        }
    }
}
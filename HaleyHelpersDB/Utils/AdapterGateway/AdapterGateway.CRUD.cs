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
        public Task<IFeedback<DbRows>> ReadAsync(IAdapterArgs input, params (string key, object value)[] parameters) => SafeInternalExecute((inp) => GetAdapter(inp).ReadAsync(inp, parameters),input);

        public Task<IFeedback<DbRow>> ReadSingleAsync(IAdapterArgs input, params (string key, object value)[] parameters) => SafeInternalExecute((inp) => GetAdapter(inp).ReadSingleAsync(inp, parameters), input);

        public Task<IFeedback<T>> ScalarAsync<T>(IAdapterArgs input, params (string key, object value)[] parameters) => SafeInternalExecute((inp) => GetAdapter(inp).ScalarAsync<T>(inp, parameters), input);

        public Task<IFeedback<int>> NonQueryAsync(IAdapterArgs input, params (string key, object value)[] parameters) => SafeInternalExecute((inp) => GetAdapter(inp).NonQueryAsync(inp, parameters), input);

        public Task<IFeedback<IReadOnlyList<T>>> ListAsync<T>(IAdapterArgs input, params (string key, object value)[] parameters) => SafeInternalExecute((inp) => GetAdapter(inp).ListAsync<T>(inp, parameters),input);
        

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
                if (ThrowCRUDExceptions) throw;
                var fb = new Feedback<T>();
                return Task.FromResult(fb.SetStatus(false).SetMessage(ex.Message)); // adapt to your API
            }
        }
    }
}
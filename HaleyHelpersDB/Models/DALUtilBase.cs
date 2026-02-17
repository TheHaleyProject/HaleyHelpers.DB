using Haley.Abstractions;
using Haley.Models;
using Haley.Internal;
using System.Threading;

namespace Haley.Utils {
    public class DALUtilBase : IDALUtilBase {
        private readonly IAdapterGateway _agw;
        protected readonly string _key;

        public DALUtilBase(IAdapterGateway agw, string? adapterKey = null) {
            _agw = agw ?? throw new ArgumentNullException(nameof(agw));
            if (string.IsNullOrWhiteSpace(adapterKey)) {
                if (string.IsNullOrWhiteSpace(agw.GetDefaultKey())) throw new ArgumentNullException(nameof(adapterKey));
                _key = agw.GetDefaultKey();
            } else {
                _key = adapterKey;
            }
        }

        public Task<int> ExecAsync(string sql, DbExecutionLoad load = default, params DbArg[] args) => _agw.ExecAsync(_key, sql, load, args);
        public Task<T?> ScalarAsync<T>(string sql, DbExecutionLoad load = default, params DbArg[] args) => _agw.ScalarAsync<T>(_key, sql, load: load, args);
        public Task<DbRow?> RowAsync(string sql, DbExecutionLoad load = default, params DbArg[] args) => _agw.RowAsync(_key, sql, load, args);
        public Task<DbRows> RowsAsync(string sql, DbExecutionLoad load = default, params DbArg[] args) => _agw.RowsAsync(_key, sql, load, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ITransactionHandler CreateNewTransaction() {
            return _agw.GetTransactionHandler(_key);
        }
    }
}

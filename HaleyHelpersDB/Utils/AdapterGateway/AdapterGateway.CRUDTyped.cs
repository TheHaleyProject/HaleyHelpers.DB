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
        public async Task<int> ExecAsync(string key, string sql, DbExecutionLoad load = default, params DbArg[] args) {
            load.Ct.ThrowIfCancellationRequested();
            var fb = await NonQueryAsync(new AdapterArgs(key) { Query = sql }.ForTransaction(load.Handler, false), args.ToAgwArgs());
            if (!fb.Status) throw new InvalidOperationException(fb.Message ?? "NonQuery failed.");
            return fb.Result;
        }
        public async Task<T?> ScalarAsync<T>(string key, string sql, DbExecutionLoad load = default, params DbArg[] args) {
            load.Ct.ThrowIfCancellationRequested();
            var fb = await ScalarAsync<T>(new AdapterArgs(key) { Query = sql }.ForTransaction(load.Handler, false), args.ToAgwArgs());
            if (!fb.Status) throw new InvalidOperationException(fb.Message ?? "Scalar failed.");
            return fb.Result;
        }
        public async Task<DbRow?> RowAsync(string key, string sql, DbExecutionLoad load = default, params DbArg[] args) {
            load.Ct.ThrowIfCancellationRequested();
            var fb = await ReadSingleAsync(new AdapterArgs(key) { Query = sql }.ForTransaction(load.Handler, false), args.ToAgwArgs());
            if (!fb.Status) throw new InvalidOperationException(fb.Message ?? "ReadSingle failed.");
            return fb.Result;
        }
        public async Task<DbRows> RowsAsync(string key, string sql, DbExecutionLoad load = default, params DbArg[] args) {
            load.Ct.ThrowIfCancellationRequested();
            var fb = await ReadAsync(new AdapterArgs(key) { Query = sql }.ForTransaction(load.Handler, false), args.ToAgwArgs());
            if (!fb.Status) throw new InvalidOperationException(fb.Message ?? "Read failed.");
            return fb.Result;
        }

        public Task<int> ExecAsync(string sql, DbExecutionLoad load = default, params DbArg[] args) => ExecAsync(_defaultAdapterKey,sql, load, args);
        public Task<T?> ScalarAsync<T>(string sql, DbExecutionLoad load = default, params DbArg[] args) => ScalarAsync<T>(_defaultAdapterKey, sql, load, args);
        public Task<DbRow?> RowAsync(string sql, DbExecutionLoad load = default, params DbArg[] args) => RowAsync(_defaultAdapterKey, sql,load, args);
        public Task<DbRows> RowsAsync(string sql, DbExecutionLoad load = default, params DbArg[] args) =>RowsAsync(_defaultAdapterKey,sql,load, args);
    }
}
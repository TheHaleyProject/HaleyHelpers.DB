using Haley.Abstractions;
using Haley.Enums;
using Haley.Utils;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class DBAdapter : IDBAdapter {
        public IAdapterConfig Info { get; }  //Read only.
        internal ISqlHandler SQLHandler { get; }

        public Guid Id { get; }

        //ConcurrentDictionary<TargetDB, ISqlHandler> _handlers = new ConcurrentDictionary<TargetDB, ISqlHandler>();
        #region Public Methods

        ISqlHandler GetHandler(TargetDB target,string constr) {
            switch (target) {
                case TargetDB.maria:
                case TargetDB.mysql:
                return new MysqlHandler(constr);
                case TargetDB.mssql:
                return new MssqlHandler(constr);
                case TargetDB.pgsql:
                return new PgsqlHandler(constr);
                case TargetDB.sqlite:
                return new SqliteHandler(constr);
                case TargetDB.unknown:
                default:
                throw new ArgumentException($@"Unable to find any matching SQL Handler for the given target : {target}");
            }
        }

        public Task<object> Scalar(IAdapterArgs input, params (string key, object value)[] parameters) => SQLHandler.Scalar(input, parameters);

        public async Task<object> Read(IAdapterArgs input, params (string key, object value)[] parameters) {
            var dset = await SQLHandler.Read(input, parameters);
            if (dset == null) return null;
           var result =  ((DataSet)dset)?.Select(true)?.Convert(input.JsonStringAsNode)?.ToList();
            //We can apply filter here itself to reduce the overheads..
            return result?.ApplyFilter(input.Filter) ?? null;
        } 

        public Task<object> NonQuery(IAdapterArgs input, params (string key, object value)[] parameters) => SQLHandler.NonQuery(input, parameters);

        public void UpdateDBEntry(IAdapterConfig newentry) {
            Info.Update(newentry);
        }

        #region Typed Handlers 
        public Task<IFeedback<DbRows>> ReadAsync(string key, string query,  params (string key, object value)[] parameters) {
            return ReadAsync(new AdapterArgs(key) { Query = query }, parameters);
        }
        public Task<IFeedback<DbRow>> ReadSingleAsync(string key, string query,  params (string key, object value)[] parameters) {
            return ReadSingleAsync(new AdapterArgs(key) { Query = query, Filter = ResultFilter.FirstDictionary }, parameters);
        }
        public Task<IFeedback<T>> ScalarAsync<T>(string key, string query,  params (string key, object value)[] parameters) {
            return ScalarAsync<T>(new AdapterArgs(key) { Query = query }, parameters);
        }
        public Task<IFeedback<int>> NonQueryAsync(string key, string query,  params (string key, object value)[] parameters) {
            return NonQueryAsync(new AdapterArgs(key) { Query = query }, parameters);
        }

        public async Task<IFeedback<DbRows>> ReadAsync(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<DbRows>();
            var result = await Read(input, parameters);
            //If result is not dictionary, then we can throw error.. But if result is empty list, then we can return empty DbRows.
            if (result is not List<Dictionary<string, object>> list) return fb.SetMessage("Invalid result returned from database for ReadAsync.");
            return fb.SetStatus(true).SetResult(list.ToDbRows());
        }

        public async Task<IFeedback<DbRow>> ReadSingleAsync(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<DbRow>();
            if (input is AdapterArgs ex) ex.Filter = ResultFilter.FirstDictionary; //We are setting result filter here itself.. 
            var result = await Read(input, parameters);
            if (result == null) return fb.SetStatus(true).SetMessage("No records found.");
            if (result is not Dictionary<string, object> dic) return fb.SetMessage("Invalid result returned from database for ReadAsync.");
            return fb.SetStatus(true).SetResult(dic.ToDbRow());
        }

        public async Task<IFeedback<T>> ScalarAsync<T>(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<T>();
            var result = await Scalar(input, parameters);
            if (result == null) return fb.SetStatus(true).SetMessage("No result returned."); //if result is null, we still need to return the result, we cannot call it as false. May be leave the result empty for the application to process.

            // bool first (covers BIT(1), TINYINT(1), string, byte[])
            if (typeof(T) == typeof(bool)) {
                if (TryToBool(result, out var bv)) return fb.SetStatus(true).SetResult((T)(object)bv);
                return fb.SetMessage($"Unexpected scalar type for bool. Got {result.GetType().Name} value '{result}'.");
            }

            // Fast-path numeric conversions commonly used (int/long)
            if (typeof(T) == typeof(long) && long.TryParse(result.ToString(), out var l))
                return fb.SetStatus(true).SetResult((T)(object)l);
            if (typeof(T) == typeof(int) && int.TryParse(result.ToString(), out var i))
                return fb.SetStatus(true).SetResult((T)(object)i);

            if (result is T typed) return fb.SetStatus(true).SetResult(typed);
            return fb.SetMessage($"Unexpected scalar type. Expected {typeof(T).Name}, got {result.GetType().Name}.");
        }

        public async Task<IFeedback<int>> NonQueryAsync(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<int>();
            var result = await NonQuery(input, parameters);
            if (result == null || !(result is int resInt)) return fb.SetStatus(false).SetResult(0);
            return fb.SetStatus(true).SetResult(resInt);
        }

        #endregion

        static bool TryToBool(object value, out bool b) {
            switch (value) {
                case bool vb: b = vb; return true;
                case byte by: b = by != 0; return true;                 // TINYINT(1)
                case sbyte sby: b = sby != 0; return true;
                case short sh: b = sh != 0; return true;
                case ushort ush: b = ush != 0; return true;
                case int vi: b = vi != 0; return true;
                case uint u: b = u != 0; return true;
                case long vl: b = vl != 0; return true;
                case ulong ul: b = ul != 0; return true;
                case decimal dec: b = dec != 0m; return true;
                case double d: b = d != 0d; return true;
                case float f: b = f != 0f; return true;
                case byte[] arr when arr.Length > 0: b = arr[0] != 0; return true; // BIT(1) as bytes
                case string s:
                if (bool.TryParse(s, out var bp)) { b = bp; return true; }
                if (long.TryParse(s, out var ln)) { b = ln != 0; return true; }
                break;
            }
            b = default;
            return false;
        }

        #endregion

        //If root config key is null, then update during run-time is not possible.
        internal DBAdapter(IAdapterConfig entry) {
            Info = entry;
            SQLHandler = GetHandler(Info.DBType,entry.ConnectionString);
            Id = Guid.NewGuid();
        }
    }
}

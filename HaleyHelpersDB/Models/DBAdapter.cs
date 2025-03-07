using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class DBAdapter : IDBAdapter {
        public IDBAdapterInfo Entry { get; }  //Read only.
        ConcurrentDictionary<TargetDB, ISqlHandler> _handlers = new ConcurrentDictionary<TargetDB, ISqlHandler>();
        #region Public Methods

        ISqlHandler GetHanlder(TargetDB target) {
            if (!_handlers.ContainsKey(target)) {
                ISqlHandler handler = null;
                switch (target) {
                    case TargetDB.maria:
                    case TargetDB.mysql:
                    handler = new MysqlHandler();
                    break;
                    case TargetDB.mssql:
                    handler = new MssqlHandler();
                    break;
                    case TargetDB.pgsql:
                    handler = new PgsqlHandler();
                    break;
                    case TargetDB.sqlite:
                    handler = new SqliteHandler();
                    break;
                    case TargetDB.unknown:
                    default:
                    throw new ArgumentException($@"Unable to find any matching SQL Handler for the given target : {target}");
                }
                _handlers.TryAdd(target,handler);
            }
            if (!_handlers.ContainsKey(target) || _handlers[target] ==null ) throw new ArgumentException($@"Unable to find any matching SQL Handler for the given target : {target}");
            return _handlers[target];
        }

        public async Task<object> ExecuteScalar(IDBInput input, params (string key, object value)[] parameters) {
            input.Conn = Entry.ConnectionString;
            return await GetHanlder(Entry.DBType).ExecuteScalar(input, parameters);
            throw new NotImplementedException("No handler found for the given DB Type");
        }

        public async Task<DataSet> ExecuteReader(IDBInput input, params (string key, object value)[] parameters) {
            input.Conn = Entry.ConnectionString;
            return await GetHanlder(Entry.DBType).ExecuteReader(input, parameters);
        }

        public async Task<object> ExecuteNonQuery(IDBInput input, params (string key, object value)[] parameters) {
            input.Conn = Entry.ConnectionString;
            return await GetHanlder(Entry.DBType).ExecuteNonQuery(input, parameters);
        }

        public void UpdateDBEntry(IDBAdapterInfo newentry) {
            Entry.Update(newentry);
        }

        #endregion

        //If root config key is null, then update during run-time is not possible.
        public DBAdapter(IDBAdapterInfo entry) { Entry = entry;  }
    }
}

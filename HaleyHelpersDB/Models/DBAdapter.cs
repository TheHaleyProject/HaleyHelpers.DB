using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class DBAdapter : IDBAdapter {
        public IDBAdapterInfo Entry { get; }  //Read only.
        ISqlHandler Handler { get; }
        //ConcurrentDictionary<TargetDB, ISqlHandler> _handlers = new ConcurrentDictionary<TargetDB, ISqlHandler>();
        #region Public Methods

        ISqlHandler GetHanlder(TargetDB target) {
            switch (target) {
                case TargetDB.maria:
                case TargetDB.mysql:
                return new MysqlHandler();
                case TargetDB.mssql:
                return new MssqlHandler();
                case TargetDB.pgsql:
                return new PgsqlHandler();
                case TargetDB.sqlite:
                return new SqliteHandler();
                case TargetDB.unknown:
                default:
                throw new ArgumentException($@"Unable to find any matching SQL Handler for the given target : {target}");
            }
        }

        public async Task<object> ExecuteScalar(IDBInput input, params (string key, object value)[] parameters) {
            input.Conn = Entry.ConnectionString;
            return await Handler.ExecuteScalar(input, parameters);
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
        public DBAdapter(IDBAdapterInfo entry) { 
            Entry = entry;
            Handler = GetHanlder(Entry.DBType);
        }
    }
}

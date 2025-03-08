using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class DBAdapter : IDBAdapter {
        public IDBAdapterInfo Info { get; }  //Read only.
        internal ISqlHandler Handler { get; }
        //ConcurrentDictionary<TargetDB, ISqlHandler> _handlers = new ConcurrentDictionary<TargetDB, ISqlHandler>();
        #region Public Methods

        ISqlHandler GetHandler(TargetDB target,bool mode) {
            switch (target) {
                case TargetDB.maria:
                case TargetDB.mysql:
                return new MysqlHandler(mode);
                case TargetDB.mssql:
                return new MssqlHandler(mode);
                case TargetDB.pgsql:
                return new PgsqlHandler(mode);
                case TargetDB.sqlite:
                return new SqliteHandler(mode);
                case TargetDB.unknown:
                default:
                throw new ArgumentException($@"Unable to find any matching SQL Handler for the given target : {target}");
            }
        }

        public async Task<object> Scalar(IDBInput input, params (string key, object value)[] parameters) {
            input.Conn = Info.ConnectionString;
            return await Handler.Scalar(input, parameters);
        }

        public async Task<object> Read(IDBInput input, params (string key, object value)[] parameters) {
            input.Conn = Info.ConnectionString;
            return await Handler.Read(input, parameters);
        }

        public async Task<object> NonQuery(IDBInput input, params (string key, object value)[] parameters) {
            input.Conn = Info.ConnectionString;
            return await Handler.NonQuery(input, parameters);
        }

        public void UpdateDBEntry(IDBAdapterInfo newentry) {
            Info.Update(newentry);
        }

        #endregion

        //If root config key is null, then update during run-time is not possible.
        public DBAdapter(IDBAdapterInfo entry): this (entry,false) { 
        }
        internal DBAdapter(IDBAdapterInfo entry, bool transactionMode) {
            Info = entry;
            Handler = GetHandler(Info.DBType,transactionMode);
        }
    }
}

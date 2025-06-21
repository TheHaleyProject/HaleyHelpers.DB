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

        public Task<object> Read(IAdapterArgs input, params (string key, object value)[] parameters) => SQLHandler.Read(input, parameters);

        public Task<object> NonQuery(IAdapterArgs input, params (string key, object value)[] parameters) => SQLHandler.NonQuery(input, parameters);

        public void UpdateDBEntry(IAdapterConfig newentry) {
            Info.Update(newentry);
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

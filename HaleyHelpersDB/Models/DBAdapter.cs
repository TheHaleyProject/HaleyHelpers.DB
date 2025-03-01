using Haley.Abstractions;
using Haley.Enums;
using System.Data;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class DBAdapter : IDBAdapter {
        public IDBAdapterInfo Entry { get; }  //Read only.
        #region Public Methods

        public async Task<DataSet> ExecuteReader(IDBInput input, params (string key, object value)[] parameters) {
            input.Conn = Entry.ConnectionString;
            switch (Entry.DBType) {
                case TargetDB.mssql: //Microsoft SQL
                return await MssqlHandler.ExecuteReader(input, parameters);
                case TargetDB.pgsql: //Postgres
                return await PgsqlHandler.ExecuteReader(input, parameters);
                case TargetDB.maria: //Mariadb
                case TargetDB.mysql:
                return await MysqlHandler.ExecuteReader(input, parameters);
                case TargetDB.sqlite:
                return await SqliteHandler.ExecuteReader(input, parameters);
            }
            throw new NotImplementedException("No handler found for the given DB Type");
        }

        public async Task<object> ExecuteNonQuery(IDBInput input, params (string key, object value)[] parameters) {
            input.Conn = Entry.ConnectionString;
            switch (Entry.DBType) {
                case TargetDB.mssql: //Microsoft SQL
                return await MssqlHandler.ExecuteNonQuery(input, parameters);
                case TargetDB.pgsql: //Postgres
                return await PgsqlHandler.ExecuteNonQuery(input, parameters);
                case TargetDB.maria: //Mariadb
                case TargetDB.mysql:
                return await MysqlHandler.ExecuteNonQuery(input, parameters);
                case TargetDB.sqlite: //Mariadb
                return await SqliteHandler.ExecuteNonQuery(input, parameters);
            }
            throw new NotImplementedException("No handler found for the given DB Type");
        }

        public void UpdateDBEntry(IDBAdapterInfo newentry) {
            Entry.Update(newentry);
        }

        #endregion

        //If root config key is null, then update during run-time is not possible.
        public DBAdapter(IDBAdapterInfo entry) { Entry = entry;  }
    }
}

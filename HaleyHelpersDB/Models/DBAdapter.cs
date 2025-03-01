using Haley.Utils;
using System.Collections.Concurrent;
using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Haley.Enums;
using System.Reflection.Metadata.Ecma335;
using System.Configuration;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class DBAdapter
    {
        public DbaEntry Entry { get; }  //Read only.
        #region Public Methods

        public async Task<DataSet> ExecuteReader(DBSInput input, params (string key, object value)[] parameters) {
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

        public async Task<object> ExecuteNonQuery(DBSInput input, params (string key, object value)[] parameters) {
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

        public void UpdateDBEntry(DbaEntry newentry) {
            Entry.Update(newentry);
        }

        #endregion

        //If root config key is null, then update during run-time is not possible.
        public DBAdapter(DbaEntry entry) { Entry = entry;  }
    }
}

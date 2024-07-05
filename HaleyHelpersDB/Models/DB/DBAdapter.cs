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

        public async Task<DataSet> ExecuteReader(DBInput input, params (string key, object value)[] parameters) {
            input.Conn = Entry.ConnectionString;
            switch (Entry.DBType) {
                case TargetDB.mssql: //Microsoft SQL
                return await MssqlHandler.ExecuteReader(input, parameters);
                case TargetDB.pgsql: //Postgres
                return await PgsqlHandler.ExecuteReader(input, parameters);
                case TargetDB.maria: //Mariadb
                return await MysqlHandler.ExecuteReader(input, parameters);
            }
            return await MysqlHandler.ExecuteReader(input, parameters);
        }

        public async Task<object> ExecuteNonQuery(DBInput input, params (string key, object value)[] parameters) {
            input.Conn = Entry.ConnectionString;
            switch (Entry.DBType) {
                case TargetDB.mssql: //Microsoft SQL
                return await MssqlHandler.ExecuteNonQuery(input, parameters);
                case TargetDB.pgsql: //Postgres
                return await PgsqlHandler.ExecuteNonQuery(input, parameters);
                case TargetDB.maria: //Mariadb
                return await MysqlHandler.ExecuteNonQuery(input, parameters);
            }
            return await MysqlHandler.ExecuteNonQuery(input, parameters);
        }

        public void UpdateDBEntry(DbaEntry newentry) {
            Entry.Update(newentry);
        }

        #endregion

        //If root config key is null, then update during run-time is not possible.
        public DBAdapter(DbaEntry entry) { Entry = entry;  }
    }
}

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

        public async Task<DataSet> ExecuteReader(string query, ILogger logger, params (string key, object value)[] parameters) {
            switch (Entry.DBType) {
                case TargetDB.mssql: //Microsoft SQL
                return await MssqlHandler.ExecuteReader(Entry.ConnectionString, query, logger, parameters);
                case TargetDB.pgsql: //Postgres
                return await PgsqlHandler.ExecuteReader(Entry.ConnectionString,query, logger, parameters);
                case TargetDB.maria: //Mariadb
                return await MysqlHandler.ExecuteReader(Entry.ConnectionString, query, logger, parameters);
            }
            return await MysqlHandler.ExecuteReader(Entry.ConnectionString, query, logger, parameters);
        }

        public async Task<int> ExecuteNonQuery(string query, ILogger logger, params (string key, object value)[] parameters) {
            switch (Entry.DBType) {
                case TargetDB.mssql: //Microsoft SQL
                return await MssqlHandler.ExecuteNonQuery(Entry.ConnectionString, query, logger, parameters);
                case TargetDB.pgsql: //Postgres
                return await PgsqlHandler.ExecuteNonQuery(Entry.ConnectionString, query, logger, parameters);
                case TargetDB.maria: //Mariadb
                return await MysqlHandler.ExecuteNonQuery(Entry.ConnectionString, query, logger, parameters);
            }
            return await MysqlHandler.ExecuteNonQuery(Entry.ConnectionString, query, logger, parameters);
        }

        public void UpdateDBEntry(DbaEntry newentry) {
            Entry.Update(newentry);
        }

        #endregion

        //If root config key is null, then update during run-time is not possible.
        public DBAdapter(DbaEntry entry) { Entry = entry;  }
    }
}

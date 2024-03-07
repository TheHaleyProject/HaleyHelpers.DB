using Haley.Utils;
using System.Collections.Concurrent;
using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Haley.Enums;
using System.Reflection.Metadata.Ecma335;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class DBAdapter
    {
        string _jsonKey; // Internal config Key
        string _targetConn; //Connection string for the selected connection
        TargetDB _targetDB = TargetDB.mysql;

        #region Public Methods

        public async Task<DataSet> ExecuteReader(string query, ILogger logger, params (string key, object value)[] parameters) {
            switch (_targetDB) {
                case TargetDB.mssql: //Microsoft SQL
                return await MssqlHandler.ExecuteReader(_targetConn,query, logger, parameters);
                case TargetDB.pgsql: //Postgres
                return await PgsqlHandler.ExecuteReader(_targetConn,query, logger, parameters);
                case TargetDB.maria: //Mariadb
                return await MysqlHandler.ExecuteReader(_targetConn, query, logger, parameters);
            }
            return await MysqlHandler.ExecuteReader(_targetConn, query, logger, parameters);
        }

        public async Task<int> ExecuteNonQuery(string query, ILogger logger, params (string key, object value)[] parameters) {
            switch (_targetDB) {
                case TargetDB.mssql: //Microsoft SQL
                return await MssqlHandler.ExecuteNonQuery(_targetConn, query, logger, parameters);
                case TargetDB.pgsql: //Postgres
                return await PgsqlHandler.ExecuteNonQuery(_targetConn, query, logger, parameters);
                case TargetDB.maria: //Mariadb
                return await MysqlHandler.ExecuteNonQuery(_targetConn, query, logger, parameters);
            }
            return await MysqlHandler.ExecuteNonQuery(_targetConn, query, logger, parameters);
        }

        public (string key, string dbName, TargetDB dbtype) GetInfo() {
            string database = null;
            if (string.IsNullOrWhiteSpace(_targetConn)) return (_jsonKey, database, _targetDB);
            var dbInfo = _targetConn.Split(';')?.FirstOrDefault(p => p.StartsWith("database="));
            if (dbInfo != null) {
                database = dbInfo.Substring("database=".Length).Trim();
            }

            return (_jsonKey, database,_targetDB);
        }

        public void UpdateConnectionString(string connectionStr, TargetDB dbtype) {
            _targetConn = connectionStr;
            _targetDB = dbtype;
        }

        #endregion

        //If root config key is null, then update during run-time is not possible.
        public DBAdapter(string connectionStr, string rootCfg_key,TargetDB dbtype) { _jsonKey = rootCfg_key; UpdateConnectionString(connectionStr, dbtype); }
    }
}

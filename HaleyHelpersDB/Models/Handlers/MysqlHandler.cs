﻿using Haley.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
//using MySql.Data.MySqlClient;
using MySqlConnector;
using System.Data;
using System.Data.Common;

namespace Haley.Models {

    internal class MysqlHandler : SqlHandlerBase {
        protected override IDbCommand GetCommand(object connection) {
            if (connection is MySqlConnection sqlc) return sqlc.CreateCommand();
            return null;
        }

        protected override object GetConnection(string conStr) {
            if (_transaction != null) return _connection; //use the same connection 
            return new MySqlConnection(conStr);
        }

        protected override IDbDataParameter GetParameter() {
            return new MySqlParameter();
        }
        public MysqlHandler(string constring) : base(constring) { }
    }
}
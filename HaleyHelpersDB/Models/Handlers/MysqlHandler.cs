﻿using Haley.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
//using MySql.Data.MySqlClient;
using MySqlConnector;
using System.Data;
using System.Data.Common;

namespace Haley.Models {

    internal class MysqlHandler : SqlHandlerBase {

        protected override object GetConnection(string conStr, bool forTransaction) {
            if (_transaction != null) return _connection; //use the same connection 
            return new MySqlConnection(conStr);
        }
        protected override void FillParameterInternal(IDbDataParameter msp, object pvalue) {
            throw new NotImplementedException();
        }

        protected override IDbDataParameter GetParameter() {
            return new MySqlParameter();
        }
        public MysqlHandler(string constring) : base(constring) { }
    }
}
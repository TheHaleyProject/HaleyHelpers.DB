﻿using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Data;
using Haley.Abstractions;
using System.Data.Common;
using MySqlConnector;

namespace Haley.Models {

    internal class SqliteHandler : SqlHandlerBase {
        protected override string ProviderName { get; } = "SQLITE";
        public SqliteHandler(string constring) : base(constring) { }
        
        protected override object GetConnection(string conStr, bool forTransaction) {
            if (_transaction != null) return _connection; //use the same connection 
            return new SqliteConnection(conStr);
        }
       
        protected override IDbDataParameter GetParameter() {
            return new SqliteParameter();
        }
    }
}
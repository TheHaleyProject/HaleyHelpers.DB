using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Data;
using Haley.Abstractions;
using System.Data.Common;
using MySqlConnector;

namespace Haley.Models {

    internal class SqliteHandler : SqlHandlerBase {
        public SqliteHandler(string constring) : base(constring) { }
        protected override IDbCommand GetCommand(object connection) {
            if (connection is SqliteConnection sqlc) return sqlc.CreateCommand();
            return null;
        }

        protected override object GetConnection(string conStr) {
            if (_transaction != null) return _connection; //use the same connection 
            return new SqliteConnection(conStr);
        }

        protected override IDbDataParameter GetParameter() {
            return new SqliteParameter();
        }
    }
}
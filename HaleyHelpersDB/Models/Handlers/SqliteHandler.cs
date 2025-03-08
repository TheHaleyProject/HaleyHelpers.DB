using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Data;
using Haley.Abstractions;
using System.Data.Common;
using MySqlConnector;

namespace Haley.Models {

    internal class SqliteHandler : SqlHandlerBase {
        public SqliteHandler(bool transactionMode) : base(transactionMode) { }
        protected override IDbCommand GetCommand(object connection) {
            if (connection is SqliteConnection sqlc) return sqlc.CreateCommand();
            return null;
        }

        protected override IDisposable GetConnection(string conStr) {
            return new SqliteConnection(conStr);
        }

        protected override IDbDataParameter GetParameter() {
            return new SqliteParameter();
        }
    }
}
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Data;
using Haley.Abstractions;
using System.Data.Common;

namespace Haley.Models {

    internal class SqliteHandler : SqlHandlerBase<SqliteCommand> {
        protected override DbConnection GetConnection(string conStr) {
            return new SqliteConnection(conStr);
        }

        protected override IDbDataParameter GetParameter() {
            return new SqliteParameter();
        }
    }
}
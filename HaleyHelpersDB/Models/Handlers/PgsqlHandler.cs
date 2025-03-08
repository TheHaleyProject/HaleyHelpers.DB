using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text.RegularExpressions;
using Haley.Utils;
using Haley.Abstractions;
using Microsoft.Data.SqlClient;

namespace Haley.Models {

    internal class PgsqlHandler : SqlHandlerBase {
        public PgsqlHandler(bool transactionMode) : base(transactionMode) { }
        //NpgsqlDataSource.Create(input.Conn)
        protected override IDbCommand GetCommand(object connection) {
            if (connection is NpgsqlDataSource npgs) return npgs.CreateCommand();
            return null;
        }

        protected override IDisposable GetConnection(string conStr) {
            return NpgsqlDataSource.Create(conStr);
        }

        protected override IDbDataParameter GetParameter() {
            return new NpgsqlParameter();
        }
    }
}
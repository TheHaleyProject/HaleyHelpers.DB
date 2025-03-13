using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text.RegularExpressions;
using Haley.Utils;
using Haley.Abstractions;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace Haley.Models {

    internal class PgsqlHandler : SqlHandlerBase {
        public PgsqlHandler(string constring) : base(constring) { }
        //NpgsqlDataSource.Create(input.Conn)
        protected override IDbCommand GetCommand(object connection) {
            if (connection is NpgsqlDataSource npgs) return npgs.CreateCommand();
            return null;
        }

        protected override object GetConnection(string conStr) {
            //if (TransactionMode) return NpgsqlDataSource.Create(conStr).CreateConnection();
            if (_transaction != null) return _connection; //use the same connection 
            return NpgsqlDataSource.Create(conStr); //This will automanage everything internally
        }

        protected override IDbDataParameter GetParameter() {
            return new NpgsqlParameter();
        }
    }
}
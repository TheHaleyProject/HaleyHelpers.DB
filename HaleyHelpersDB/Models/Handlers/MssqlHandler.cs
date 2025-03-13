using Haley.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace Haley.Models {

    internal class MssqlHandler : SqlHandlerBase {
        public MssqlHandler(string constring) : base(constring) { }
        protected override IDbCommand GetCommand(object connection) {
            if (connection is SqlConnection sqlc) return sqlc.CreateCommand();
            return null;
        }

        // https://stackoverflow.com/questions/35928312/c-sharp-mysqlcommand-executenonquery-return-1
        protected override object GetConnection(string conStr) {
            if (_transaction != null) return _connection; //use the same connection 
            return new SqlConnection(conStr);
        }

        protected override IDbDataParameter GetParameter() {
            return new SqlParameter();
        }
    }
}
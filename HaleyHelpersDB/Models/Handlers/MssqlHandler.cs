using Haley.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace Haley.Models {

    internal class MssqlHandler : SqlHandlerBase<SqlCommand> {
        // https://stackoverflow.com/questions/35928312/c-sharp-mysqlcommand-executenonquery-return-1
        protected override DbConnection GetConnection(string conStr) {
            return new SqlConnection(conStr);
        }

        protected override IDbDataParameter GetParameter() {
            return new SqlParameter();
        }
    }
}
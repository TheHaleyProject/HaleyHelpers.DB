using Haley.Abstractions;
using Microsoft.Extensions.Logging;
//using MySql.Data.MySqlClient;
using MySqlConnector;
using System.Data;
using System.Data.Common;

namespace Haley.Models {

    internal class MysqlHandler : SqlHandlerBase<MySqlCommand> {
        protected override DbConnection GetConnection(string conStr) {
            return new MySqlConnection(conStr);
        }

        protected override IDbDataParameter GetParameter() {
            return new MySqlParameter();
        }
    }
}
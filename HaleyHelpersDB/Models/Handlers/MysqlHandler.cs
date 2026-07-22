using Haley.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
//using MySql.Data.MySqlClient;
using MySqlConnector;
using System.Data;
using System.Data.Common;

namespace Haley.Models {

    internal class MysqlHandler : SqlHandlerBase {
        protected override string ProviderName { get; } = "MYSQL";

        protected override object GetConnection(ConInfo conInfo, bool forTransaction) {
            if (_transaction != null) return _connection; //use the same connection 
            if (conInfo.IgnoreSsl == true) {
                var builder = new MySqlConnectionStringBuilder(conInfo.ConString) {
                    SslMode = MySqlSslMode.None
                };
                return new MySqlConnection(builder.ConnectionString);
            }
            return new MySqlConnection(conInfo.ConString);
        }

        protected override IDbDataParameter GetParameter() {
            return new MySqlParameter();
        }

        public MysqlHandler(ConInfo conInfo) : base(conInfo) { }
    }
}

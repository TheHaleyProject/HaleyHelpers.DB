using Haley.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace Haley.Models {

    internal class MssqlHandler : SqlHandlerBase {
        protected override string ProviderName { get; } = "MSSQL";
        public MssqlHandler(ConInfo conInfo) : base(conInfo) { }

        // https://stackoverflow.com/questions/35928312/c-sharp-mysqlcommand-executenonquery-return-1
        protected override object GetConnection(ConInfo conInfo, bool forTransaction) {
            if (_transaction != null) return _connection; //use the same connection 
            return new SqlConnection(conInfo.ConString);
        }

        protected override IDbDataParameter GetParameter() {
            return new SqlParameter();
        }
    }
}

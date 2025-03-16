using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text.RegularExpressions;
using Haley.Utils;
using Haley.Abstractions;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using NpgsqlTypes;
using System.Runtime.CompilerServices;

namespace Haley.Models {

    internal class PgsqlHandler : SqlHandlerBase {
        public PgsqlHandler(string constring) : base(constring) { }
        //NpgsqlDataSource.Create(input.Conn)
        protected override IDbCommand GetCommand(object connection) {
            if (connection is NpgsqlDataSource npgs) return npgs.CreateCommand();
            return base.GetCommand(connection);
        }
        protected override void FillParameterInternal(IDbDataParameter msp, object pvalue) {
            if (msp is NpgsqlParameter npsp) {
                var tup = (ITuple)pvalue;
                msp.Value = tup[0] ?? DBNull.Value;
                if (tup.Length > 1 && tup[1] is NpgsqlDbType dbt) npsp.NpgsqlDbType = dbt;
            } else {
                throw new NotImplementedException();
            }
        }
        protected override object GetConnection(string conStr, bool forTransaction) {
            //if (TransactionMode) return NpgsqlDataSource.Create(conStr).CreateConnection();
            if (_transaction != null) return _connection; //use the same connection 
            if (forTransaction) return NpgsqlDataSource.Create(conStr).CreateConnection();
            return NpgsqlDataSource.Create(conStr); //This will automanage everything internally
        }

        protected override IDbDataParameter GetParameter() {
            return new NpgsqlParameter();
        }
    }
}
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Haley.Models {

    public static class MssqlHandler {
        // https://stackoverflow.com/questions/35928312/c-sharp-mysqlcommand-executenonquery-return-1
        public static async Task<int> ExecuteNonQuery(string targetConn, string query, ILogger logger, params (string key, object value)[] parameters) {
            var result = await ExecuteInternal(targetConn, query, logger, async (cmd) => {
                int status = 0;
                //exceute non-query will return -1 for all calls other than insert/update/delete, as the return value is the number of rows affected.
                //if we are using stored procedures, it will always return -1.
                status = await cmd.ExecuteNonQueryAsync();
                return status;
            }, parameters);
            if (result?.GetType() == typeof(int)) { return int.Parse(result.ToString()! ?? "0"); }
            return 0;
        }

        public static async Task<DataSet> ExecuteReader(string targetConn, string query, ILogger logger, params (string key, object value)[] parameters) {
            var result = await ExecuteInternal(targetConn, query, logger, async (cmd) => {
                var reader = await cmd.ExecuteReaderAsync();
                DataSet ds = new DataSet();
                int count = 1;

                if (reader == null) return null;
                //Don't load the first one directly. It will not capture other results.
                while (!reader.IsClosed) {
                    //await reader.ReadAsync();
                    //Read all tables and return the final one.
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    //todo: put them inside a dataset and return
                    ds.Tables.Add(dt);

                    logger?.LogInformation($@"Table - {count} created.");
                    count++;
                }
                await reader.CloseAsync();
                return ds;
            }, parameters);

            return result as DataSet;
        }

        private static async Task<object> ExecuteInternal(string targetCon, string query, ILogger logger, Func<SqlCommand, Task<object>> processor, params (string key, object value)[] parameters) {
            using (SqlConnection conn = new SqlConnection(targetCon)) {
                //INITIATE CONNECTION
                logger?.LogInformation($@"Opening connection - {targetCon}");
                await conn.OpenAsync();

                var cmd = conn.CreateCommand();
                cmd.CommandText = query;
                logger?.LogInformation("Creating query");

                //ADD PARAMETERS IF REQUIRED
                if (parameters.Length > 0) {
                    SqlParameter[] msp = new SqlParameter[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++) {
                        var key = parameters[i].key;
                        if (!key.StartsWith("@")) { key = "@" + key; } //Check why this is required.
                        msp[i] = new SqlParameter(key, parameters[i].value);
                        cmd.Parameters.Add(msp[i]);
                    }
                }
                logger?.LogInformation("About to execute");
                var result = await processor.Invoke(cmd);
                await conn.CloseAsync();
                logger?.LogInformation("Connection closed");
                return result;
            }
        }
    }
}
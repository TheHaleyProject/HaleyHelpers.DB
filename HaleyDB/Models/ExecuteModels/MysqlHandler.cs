using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System.Data;

namespace Haley.Models {

    public static class MysqlHandler {

        public static async Task<int> ExecuteNonQuery(string targetConn, string query, ILogger logger, params (string key, object value)[] parameters) {
            var result = await ExecuteInternal(targetConn, query, logger, async (cmd) => {
                int status = 0;
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

                //Don't load the first result directly. It will not capture other results.
                while (!reader.IsClosed) {
                    await reader.ReadAsync();
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    ds.Tables.Add(dt);
                    logger?.LogInformation($@"Table - {count} created.");
                    count++;
                }
                await reader.CloseAsync();
                return ds;
            }, parameters);

            return result as DataSet;
        }

        private static async Task<object> ExecuteInternal(string targetCon, string query, ILogger logger, Func<MySqlCommand, Task<object>> processor, params (string key, object value)[] parameters) {
            using (var conn = new MySqlConnection() { ConnectionString = targetCon }) {
                //INITIATE CONNECTION
                logger?.LogInformation($@"Opening connection - {targetCon}");
                //conn.Open();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = query;
                logger?.LogInformation("Creating query");

                //ADD PARAMETERS IF REQUIRED
                if (parameters.Length > 0) {
                    MySqlParameter[] msp = new MySqlParameter[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++) {
                        var key = parameters[i].key;
                        if (!key.StartsWith("@")) { key = "@" + key; } //Check why this is required.
                        msp[i] = new MySqlParameter(key, parameters[i].value);
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
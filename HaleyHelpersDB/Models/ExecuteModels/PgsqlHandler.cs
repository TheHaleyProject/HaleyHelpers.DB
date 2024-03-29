using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Npgsql;
using System.Data;

namespace Haley.Models {

    public static class PgsqlHandler {

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

        private static async Task<object> ExecuteInternal(string targetCon, string query, ILogger logger, Func<NpgsqlCommand, Task<object>> processor, params (string key, object value)[] parameters) {
            using (var conn = NpgsqlDataSource.Create(targetCon)) {
                //INITIATE CONNECTION
                logger?.LogInformation($@"opening connection - {targetCon}");

                ////If schema name is available, try to set search path for this connection.
                //if (!string.IsNullOrWhiteSpace(schemaname)) {
                //    logger?.LogInformation($@"Schema name found - {schemaname}");
                //    var searchPathCmd = MakeCommand(conn, $@"set search_path to {schemaname}",logger);
                //    var status = await searchPathCmd.ExecuteNonQueryAsync(); //Let us first set the search path for this.
                //}

                var cmd = MakeCommand(conn, query, logger, parameters);
                logger?.LogInformation("About to execute");

                var result = await processor.Invoke(cmd);
                logger?.LogInformation("Connection closed");
                return result;
            }
        }

        private static NpgsqlCommand MakeCommand(NpgsqlDataSource conn,  string query, ILogger logger, params (string key, object value)[] parameters) {
            //await dsource.OpenConnectionAsync();
            var cmd = conn.CreateCommand(query);
            //ADD PARAMETERS IF REQUIRED
            if (parameters.Length > 0) {
                // NpgsqlParameter[] msp = new NpgsqlParameter[parameters.Length];
                for (int i = 0; i < parameters.Length; i++) {
                    var key = parameters[i].key;
                    var msp = new NpgsqlParameter(key, parameters[i].value);
                    if (!key.StartsWith("@")) { key = "@" + key; }
                    cmd.Parameters.Add(msp);
                }
            }
            return cmd;
        }
    }
}
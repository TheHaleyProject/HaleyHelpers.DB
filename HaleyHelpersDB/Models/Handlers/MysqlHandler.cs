using Haley.Abstractions;
using Microsoft.Extensions.Logging;
//using MySql.Data.MySqlClient;
using MySqlConnector;
using System.Data;

namespace Haley.Models {

    public static class MysqlHandler {

        public static async Task<object> ExecuteNonQuery(IDBInput input, params (string key, object value)[] parameters) {
            var result = await ExecuteInternal(input, async (cmd) => {

                int status = 0;
                //If command has output parameter, no need to fetch.
                // ################### TODO (Add provision for handling return methods) #############################

                status = await cmd.ExecuteNonQueryAsync();
                return status;
            }, parameters);
            if (result?.GetType() == typeof(int)) { return int.Parse(result.ToString()! ?? "0"); }
            return 0;
        }

        public static async Task<DataSet> ExecuteReader(IDBInput input, params (string key, object value)[] parameters) {
            var result = await ExecuteInternal(input, async (cmd) => {
                var reader = await cmd.ExecuteReaderAsync();
                DataSet ds = new DataSet();
                int count = 1;

                if (reader == null) return null;

                //Don't load the first result directly. It will not capture other results.
                while (!reader.IsClosed) {
                    //await reader.ReadAsync(); //This actually jumps to next table. So dont' read it.. directly start loading them in to data table both has same function
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    ds.Tables.Add(dt);
                    input.Logger?.LogInformation($@"Table - {count} created.");
                    count++;
                }
                await reader.CloseAsync();
                return ds;
            }, parameters);

            return result as DataSet;
        }

        private static async Task<object> ExecuteInternal(IDBInput input, Func<MySqlCommand, Task<object>> processor, params (string key, object value)[] parameters) {
            using (var conn = new MySqlConnection() { ConnectionString = input.Conn }) {
                //INITIATE CONNECTION
                input.Logger?.LogInformation($@"Opening connection - {input.Conn}");
                //conn.Open();
                await conn.OpenAsync();

                var cmd = conn.CreateCommand();
                cmd.CommandText = input.Query;
                input.Logger?.LogInformation("Creating query");

                //ADD PARAMETERS IF REQUIRED
                if (parameters.Length > 0) {
                    MySqlParameter[] msp = new MySqlParameter[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++) {
                        var key = parameters[i].key;
                        if (!key.StartsWith("@")) { key = "@" + key; } //Check why this is required.

                        msp[i] = new MySqlParameter();
                        msp[i].ParameterName = key;

                        bool flag = true; //start with true
                        if (input.ParamHandler != null) {
                            flag = input.ParamHandler.Invoke(key,msp[i]);
                        }
                        if (flag) {
                            msp[i].Value = parameters[i].value;
                        }
                        cmd.Parameters.Add(msp[i]);
                    }
                }
                input.Logger?.LogInformation("About to execute");
                var result = await processor.Invoke(cmd);
                await conn.CloseAsync();
                input.Logger?.LogInformation("Connection closed");
                return result;
            }
        }
    }
}
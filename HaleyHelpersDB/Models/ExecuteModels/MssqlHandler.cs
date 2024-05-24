using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System.Data;

namespace Haley.Models {

    public static class MssqlHandler {
        // https://stackoverflow.com/questions/35928312/c-sharp-mysqlcommand-executenonquery-return-1
        public static async Task<object> ExecuteNonQuery(DBInput input, params (string key, object value)[] parameters) {
            var result = await ExecuteInternal(input, async (cmd) => {
                int status = 0;
                 
                // ################### TODO (Add provision for handling return methods) #############################
               
                status = await cmd.ExecuteNonQueryAsync();
                return status;
            }, parameters);
            if (result?.GetType() == typeof(int)) { return int.Parse(result.ToString()! ?? "0"); }
            return 0;
        }

        public static async Task<DataSet> ExecuteReader(DBInput input, params (string key, object value)[] parameters) {
            var result = await ExecuteInternal(input, async (cmd) => {
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

                    input.Logger?.LogInformation($@"Table - {count} created.");
                    count++;
                }
                await reader.CloseAsync();
                return ds;
            }, parameters);

            return result as DataSet;
        }

        private static async Task<object> ExecuteInternal(DBInput input, Func<SqlCommand, Task<object>> processor, params (string key, object value)[] parameters) {
            using (SqlConnection conn = new SqlConnection(input.Conn)) {
                //INITIATE CONNECTION
                input.Logger?.LogInformation($@"Opening connection - {input.Conn}");
                await conn.OpenAsync();

                var cmd = conn.CreateCommand();
                cmd.CommandText = input.Query;
                input.Logger?.LogInformation("Creating query");

                //ADD PARAMETERS IF REQUIRED
                if (parameters.Length > 0) {
                    SqlParameter[] msp = new SqlParameter[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++) {
                        var key = parameters[i].key;
                        if (!key.StartsWith("@")) { key = "@" + key; } //Check why this is required.
                        //msp[i] = new SqlParameter(key, parameters[i].value) { };
                        msp[i].ParameterName = key;
                        bool flag = true; //start with true
                        if (input.ParamHandler != null) {
                            flag = input.ParamHandler.Invoke(key, msp[i]);
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
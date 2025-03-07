using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text.RegularExpressions;
using Haley.Utils;
using Haley.Abstractions;

namespace Haley.Models {

    public static class PgsqlHandler {

        private static async Task<object> ExecuteInternal(IDBInput input, Func<NpgsqlCommand, Task<object>> processor, params (string key, object value)[] parameters) {
            using (var conn = NpgsqlDataSource.Create(input.Conn)) {
                //INITIATE CONNECTION
                input.Logger?.LogInformation($@"Opening connection - {input.Conn}");

                ////If schema name is available, try to set search path for this connection.
                //if (!string.IsNullOrWhiteSpace(schemaname)) {
                //    input.Logger?.LogInformation($@"Schema name found - {schemaname}");
                //    var searchPathCmd = MakeCommand(conn, $@"set search_path to {schemaname}",input.Logger);
                //    var status = await searchPathCmd.ExecuteNonQueryAsync(); //Let us first set the search path for this.
                //}

                var cmd = MakeCommand(conn, input, parameters);
                input.Logger?.LogInformation("About to execute");

                var result = await processor.Invoke(cmd);
                input.Logger?.LogInformation("Connection closed");
                return result;
            }
        }

        private static NpgsqlCommand MakeCommand(NpgsqlDataSource conn, IDBInput input, params (string key, object value)[] parameters) {
            //await dsource.OpenConnectionAsync();
            var cmd = conn.CreateCommand(input.Query);
            //ADD PARAMETERS IF REQUIRED
            if (parameters.Length > 0) {
                // NpgsqlParameter[] msp = new NpgsqlParameter[parameters.Length];
                for (int i = 0; i < parameters.Length; i++) {
                    var key = parameters[i].key;
                    NpgsqlParameter msp = new NpgsqlParameter();
                    msp.ParameterName = key;

                    bool flag = true; //start with true
                    if (input.ParamHandler != null) {
                        flag = input.ParamHandler.Invoke(key,msp);
                    }
                    if (flag) {
                        var pvalue = parameters[i].value;
                        //if (pvalue != null &&  pvalue.GetType() == typeof(string)) {
                        //    var pvalueStr = pvalue.ToString()!;
                        //    //Uri.UnescapeDataString
                        //    pvalue = Regex.Unescape(pvalue!.ToString()).Replace("'", "''")
                        //    //pvalue = Uri.UnescapeDataString(pvalue!.ToString())
                        //    //pvalue = pvalue.ToString()
                        //    //   .Replace("'","''")
                        //    //   .Replace("\\u0027", "\\u0027\\u0027"); //If it is a string, replace the single quote.
                        //}
                        msp.Value = pvalue;
                    }
                    if (!key.StartsWith("@")) { key = "@" + key; }
                    cmd.Parameters.Add(msp);
                }
            }
            return cmd;
        }
    }
}
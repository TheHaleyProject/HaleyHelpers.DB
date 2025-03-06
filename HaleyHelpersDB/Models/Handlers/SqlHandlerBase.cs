using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text.RegularExpressions;
using Haley.Utils;
using Haley.Abstractions;
using System.Data.Common;
using MySqlConnector;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;

namespace Haley.Models {

    internal abstract class SqlHandlerBase<C> : ISqlHandler<C> where C : IDbCommand {
        public SqlHandlerBase() { }

        DbConnection GetConnection(string conStr) {
            if (typeof(C) == typeof(MySqlCommand)) return new MySqlConnection() { ConnectionString = conStr};
            if (typeof(C) == typeof(SqliteCommand)) return new SqliteConnection() { ConnectionString = conStr };
            if (typeof(C) == typeof(SqlCommand)) return new SqlConnection() { ConnectionString = conStr };
            return null;
        }

        public virtual async Task<object> ExecuteInternal(IDBInput input, Func<C, Task<object>> processor, params (string key, object value)[] parameters) {
            using (var conn = GetConnection(input.Conn)) {
                //INITIATE CONNECTION
                input.Logger?.LogInformation($@"Opening connection - {input.Conn}");
                //conn.Open();
                await conn.OpenAsync();

                IDbCommand cmd = conn.CreateCommand();
                cmd.CommandText = input.Query;
                input.Logger?.LogInformation("Creating query");

                //ADD PARAMETERS IF REQUIRED
                if (parameters.Length > 0) {
                    P[] msp = new P[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++) {
                        var key = parameters[i].key;
                        if (!key.StartsWith("@")) { key = "@" + key; } //Check why this is required.

                        msp[i] = new P();
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
                var result = await processor.Invoke((C)cmd);
                await conn.CloseAsync();
                input.Logger?.LogInformation("Connection closed");
                return result;
            }
        }

        public async Task<object> ExecuteNonQuery(IDBInput input, params (string key, object value)[] parameters) {
            try {
                var result = await ExecuteInternal(input, async (cmd) => {
                    int status = 0;
                    if (input.Prepare) {
                        await cmd.PrepareAsync();
                    }

                    //If command has output parameter, no need to fetch.
                    if (cmd.Parameters.Count > 0 && cmd.Parameters.Any(p => p.ParameterName == input.OutputName)) {
                        var reader = await cmd.ExecuteReaderAsync();
                        return cmd.Parameters.First(p => p.ParameterName == input.OutputName).Value; //return whatever we receive.
                    }

                    status = await cmd.ExecuteNonQueryAsync();
                    return status;
                }, parameters);


                if (result?.GetType() == typeof(int)) { return int.Parse(result.ToString()! ?? "0"); }
                return result ?? 0;
            } catch (Exception ex) {
                var msg = $@"Error: {ex.Message}";
                if (ex is PostgresException npEx) {
                    msg += $@", Query: {npEx.InternalQuery}";
                }
                throw new Exception(msg);
            }
        }

        public async Task<DataSet> ExecuteReader(IDBInput input, params (string key, object value)[] parameters) {
            var result = await ExecuteInternal(input, async (cmd) => {

                if (input.Prepare) {
                    await cmd.PrepareAsync();
                }

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
                    input.Logger?.LogInformation($@"For query {input.Query} - : Table Count - {count} created.");
                    count++;
                }
                await reader.CloseAsync();
                return ds;
            }, parameters);

            return result as DataSet;
        }

        public Task<object> ExecuteScalar(IDBInput input, params (string key, object value)[] parameters) {
            throw new NotImplementedException();
        }

        protected async Task<object> ExecuteInternal(IDBInput input, Func<NpgsqlCommand, Task<object>> processor, params (string key, object value)[] parameters) {
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

        private NpgsqlCommand MakeCommand(NpgsqlDataSource conn, IDBInput input, params (string key, object value)[] parameters) {
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
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
using System.Collections;

namespace Haley.Models {

    internal abstract class SqlHandlerBase<C> : ISqlHandler<C> where C : IDbCommand {
        public SqlHandlerBase() { }

        protected abstract IDisposable GetConnection(string conStr);
        protected abstract IDbCommand GetCommand(object connection);
        protected abstract IDbDataParameter GetParameter();

        protected virtual void FillParameters(IDbCommand cmd, IDBInput input, params (string key, object value)[] parameters) {
            //ADD PARAMETERS IF REQUIRED
            if (parameters.Length > 0) {
                //IDbDataParameter[] msp = new IDbDataParameter[parameters.Length];
                for (int i = 0; i < parameters.Length; i++) {
                    var key = parameters[i].key;
                    var msp = GetParameter();
                    msp.ParameterName = key;
                    bool flag = true; //start with true
                    if (input.ParamHandler != null) {
                        flag = input.ParamHandler.Invoke(key, msp);
                    }
                    if (flag) {
                        msp.Value = parameters[i].value;
                        //var pvalue = parameters[i].value;
                        //if (pvalue != null &&  pvalue.GetType() == typeof(string)) {
                        //    var pvalueStr = pvalue.ToString()!;
                        //    //Uri.UnescapeDataString
                        //    pvalue = Regex.Unescape(pvalue!.ToString()).Replace("'", "''")
                        //    //pvalue = Uri.UnescapeDataString(pvalue!.ToString())
                        //    //pvalue = pvalue.ToString()
                        //    //   .Replace("'","''")
                        //    //   .Replace("\\u0027", "\\u0027\\u0027"); //If it is a string, replace the single quote.
                        //}
                    }
                    if (!key.StartsWith("@")) { key = "@" + key; } //Check why this is required.
                    cmd.Parameters.Add(msp);
                }
            }
        }

        public virtual async Task<object> ExecuteInternal(IDBInput input, Func<IDbCommand, Task<object>> processor, params (string key, object value)[] parameters) {
            var conn = GetConnection(input.Conn);
            using (conn) {
                //INITIATE CONNECTION
                input.Logger?.LogInformation($@"Opening connection - {input.Conn}");
                //conn.Open();
                if (conn is DbConnection) await ((DbConnection)conn).OpenAsync();
                IDbCommand cmd = GetCommand(conn);
                cmd.CommandText = input.Query;
                input.Logger?.LogInformation("Creating query");
                FillParameters(cmd, input, parameters);
                input.Logger?.LogInformation("About to execute");
                var result = await processor.Invoke(cmd);
                if (conn is DbConnection) await ((DbConnection)conn).CloseAsync();
                input.Logger?.LogInformation("Connection closed");
                return result;
            }
        }

        public async Task<object> ExecuteNonQuery(IDBInput input, params (string key, object value)[] parameters) {

            try {
                var result = await ExecuteInternal(input, async (dbc) => {
                    if (!(dbc is DbCommand cmd)) return null;
                    int status = 0;
                    if (input.Prepare) {
                        await cmd.PrepareAsync();
                    }

                    //If command has output parameter, no need to fetch.
                    if (cmd.Parameters.Count > 0 &&  cmd.Parameters.Cast<IDbDataParameter>().Any(p => p.ParameterName == input.OutputName)) {
                        var reader = await cmd.ExecuteReaderAsync();
                        return cmd.Parameters.Cast<IDbDataParameter>()?.First(p => p.ParameterName == input.OutputName)?.Value; //return whatever we receive.
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
            var result = await ExecuteInternal(input, async (dbc) => {
                if (!(dbc is DbCommand cmd)) return null;
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
    }
}
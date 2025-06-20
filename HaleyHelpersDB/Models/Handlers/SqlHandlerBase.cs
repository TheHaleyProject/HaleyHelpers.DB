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
using System.Collections.Concurrent;
using System;
using NpgsqlTypes;
using System.Runtime.CompilerServices;

namespace Haley.Models {

    internal abstract class SqlHandlerBase : ISqlHandler {
        protected virtual bool IsConnectionWrapped(object conn) => false;
        protected virtual IDbCommand CreateWrappedCommand(object conn) => throw new NotImplementedException();
        protected abstract string ProviderName { get; }
        bool _disposed;
        protected string _conString;
        string TupleTypeName = typeof(ValueTuple).FullName!;
        public SqlHandlerBase(string constr) { _conString = constr; }
        protected DbConnection? _connection;
        protected IDbTransaction? _transaction; //If a transaction is available, then use it.. or else ignore it.
        protected virtual void FillParameterInternal(IDbDataParameter msp, object pvalue) {
            if (!pvalue.GetType().IsAssignableFrom(typeof(ITuple))) throw new ArgumentException("Method not implemented");
            var tup = (ITuple)pvalue;
            msp.Value = tup[0] ?? DBNull.Value;
            if (tup.Length > 1 && tup[1] is  DbType dbt) msp.DbType = dbt;
        }
        protected abstract object GetConnection(string conStr, bool forTransaction = false);
        protected virtual IDbCommand GetCommand(object connection) {
            if (IsConnectionWrapped(connection)) return CreateWrappedCommand(connection);
            if (connection is DbConnection dbc) return dbc.CreateCommand();
            throw new ArgumentException($@"Unable to create command for the given connection type : {connection.GetType()}");
        }
        protected abstract IDbDataParameter GetParameter();
        protected virtual void FillParameters(IDbCommand cmd, IAdapterParameter input, params (string key, object value)[] parameters) {
            //Here we know the query and also know the inputs. All we need to do it,just fetch the parameter sets in the query.
            //tocheck: Would it create performance issue to check this? would it take more time to do this? is this duration negligible?
            //priority 1 : parameters params
            //priority 2 : whatever inside the adapter
            //ASSUMPTION: parameters is not null inside the adapter parameter
            Dictionary<string, object> cmdparams = new Dictionary<string, object>(input.Parameters, StringComparer.InvariantCultureIgnoreCase);

            foreach (var param in parameters) {
                if (!cmdparams.ContainsKey(param.key)) {
                    cmdparams.TryAdd(param.key, param.value);
                } else {
                    cmdparams[param.key] = param.value;
                }
            }

            //ADD PARAMETERS IF REQUIRED
            if (cmdparams.Count > 0) {
                //IDbDataParameter[] msp = new IDbDataParameter[parameters.Length];
                foreach (var kvp in cmdparams) {
                    var msp = GetParameter();
                    msp.ParameterName = kvp.Key.ToUpper(); //All key should be in caps.
                    bool flag = true; //start with true
                    if (input.ParamHandler != null) {
                        flag = input.ParamHandler.Invoke(kvp.Key, msp);
                    }
                    if (flag) {
                        var pvalue = kvp.Value;
                        if (pvalue.GetType().FullName!.StartsWith(TupleTypeName)) {
                            FillParameterInternal(msp, pvalue);
                        } else {
                            msp.Value = pvalue ?? DBNull.Value; //Lets set the value as dbnull
                        }
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
                    if (!msp.ParameterName.StartsWith("@")) { msp.ParameterName = "@" + msp.ParameterName; } //Check why this is required.
                    cmd.Parameters.Add(msp);
                }
            }
        }

        public virtual async Task<object> ExecuteInternal(IAdapterParameter input, Func<IDbCommand, Task<object>> processor, params (string key, object value)[] parameters) {
            if (!(input is AdapterParameter)) throw new ArgumentException($@"Input is not derived from {nameof(AdapterParameter)}. Cannot obtain the connection string information.");
            var conn = GetConnection(_conString);
            //INITIATE CONNECTION
            input.Logger?.LogInformation($@"Opening connection - {_conString}");
            //conn.Open();
            if (input.TransactionMode) {
                if (_transaction == null)throw new ArgumentNullException("This SQL Handler will work only inside a transaction. Transaction appears to be null. Please verify if you have disposed or closed the transaction object.");
            } else if (conn is DbConnection dbc1) {
                await dbc1.OpenAsync(); //For pgsql, we don't do this. It is done by NPGSQL source
            }

            IDbCommand cmd = GetCommand(conn);
            cmd.CommandText = input.Query;
            input.Logger?.LogInformation("Creating query");
            FillParameters(cmd, input, parameters);
            input.Logger?.LogInformation("About to execute");
            var result = await processor.Invoke(cmd);

            if (!input.TransactionMode && conn is DbConnection dbc2) await dbc2.CloseAsync();
            input.Logger?.LogInformation("Connection closed");
            return result;
        }

        public async Task<object> NonQuery(IAdapterParameter input, params (string key, object value)[] parameters) {

            try {
                var result = await ExecuteInternal(input, async (dbc) => {
                    if (dbc is not DbCommand cmd)
                        throw new InvalidOperationException($"Expected DbCommand but received: {dbc.GetType().FullName}");
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

        public async Task<object> Read(IAdapterParameter input, params (string key, object value)[] parameters) {
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
                return ds; //Only return dataset
            }, parameters);

            return result as DataSet;
        }

        public async Task<object> Scalar(IAdapterParameter input, params (string key, object value)[] parameters) {
            return await ExecuteInternal(input, async (dbc) => {
                if (!(dbc is DbCommand cmd)) return null;
                if (input.Prepare) {
                    await cmd.PrepareAsync();
                }
                return await cmd.ExecuteScalarAsync();
            }, parameters);
        }

        
        public void Dispose() {
            //Will not automatically dispose. Just a means to dispose resources manually without waiting for the garbage collector
            if (_disposed) return;
            try {
                Commit();
            } finally {
                _disposed = true;
            }
        }

        public IDBTransaction Begin() {
            if (_transaction != null) throw new Exception("A transaction is already opened. Please commit/rollback the existing transaction.");
            _connection = (DbConnection)GetConnection(_conString,true);
            Task.WaitAny(_connection.OpenAsync());
            //After we get the connection, we generate the transaction.
            _transaction = _connection.BeginTransactionAsync().Result;
            return this;
        }

        public void Commit() {
            _transaction?.Commit();
            if (_connection != null) Task.WaitAny(_connection.CloseAsync());
            ClearTransactionInfo();
        }

        public void ClearTransactionInfo() {
            _transaction = null;
            _connection = null;
        }

        public void Rollback() {
            _transaction?.Rollback();
            if (_connection != null) Task.WaitAny(_connection.CloseAsync());
            ClearTransactionInfo();
        }
    }
}
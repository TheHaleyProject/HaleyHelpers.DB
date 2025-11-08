using Haley.Abstractions;
using Haley.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

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
            IDbCommand cmd = null;
            if (IsConnectionWrapped(connection)) cmd = CreateWrappedCommand(connection);
            if (connection is DbConnection dbc) cmd = dbc.CreateCommand();
            if (cmd == null) throw new ArgumentException($@"Unable to create command for the given connection type : {connection.GetType()}");
            if (_transaction != null) cmd.Transaction = _transaction;
            return cmd;
        }
        protected abstract IDbDataParameter GetParameter();
        protected virtual void FillParameters(IDbCommand cmd, IAdapterArgs input, params (string key, object value)[] parameters) {
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
                    //msp.ParameterName = kvp.Key.ToUpper(); //All key should be in caps.
                    msp.ParameterName = kvp.Key; //All key should be in caps.
                    bool flag = true; //start with true
                    if (input.ParamHandler != null) {
                        flag = input.ParamHandler.Invoke(kvp.Key, msp);
                    }
                    if (flag) {
                        var pvalue = kvp.Value;
                        if (pvalue != null && pvalue.GetType().FullName!.StartsWith(TupleTypeName)) {
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

        public virtual async Task<object> ExecuteInternal(IAdapterArgs input, Func<IDbCommand, Task<object>> processor, params (string key, object value)[] parameters) {
            object conn = null;
            try {
                if (!(input is AdapterArgs)) throw new ArgumentException($@"Input is not derived from {nameof(AdapterArgs)}. Cannot obtain the connection string information.");
                var targetConStr = _conString;
                if (input.ExcludeDBInConString) {
                    //Which means we are trying to do something without the database information.
                    //Now, in this we need to remove the database information. Because we are only trying to run the operation at the connection level. May be we are trying to create the database here.
                    targetConStr = _conString.RemoveKeys(';', "database");
                }
                conn = GetConnection(targetConStr);
                //INITIATE CONNECTION
                input.Logger?.LogInformation($@"Opening connection - {targetConStr}");
                //conn.Open();
                if (input.TransactionMode) {
                    if (_transaction == null) throw new ArgumentNullException("This SQL Handler will work only inside a transaction. Transaction appears to be null. Please verify if you have disposed or closed the transaction object.");
                } else if (conn is DbConnection dbc1) {
                    await dbc1.OpenAsync(); //For pgsql, we don't do this. It is done by NPGSQL source
                }

                object result = null;

                if (input.Query is string qryStr) {
                    IDbCommand cmd = GetCommand(conn);
                    cmd.CommandText = qryStr;
                    input.Logger?.LogInformation($@"Creating query {qryStr}");
                    FillParameters(cmd, input, parameters);
                    input.Logger?.LogInformation("About to execute");
                    result = await processor.Invoke(cmd);
                } else if (input.Query is string[] queries) {
                    foreach (var stmt in queries) {
                        string trimmed = stmt.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed)) continue; //May be a mistake.
                        IDbCommand cmd = GetCommand(conn);
                        cmd.CommandText = trimmed;
                        input.Logger?.LogInformation($@"Creating query - {trimmed}");
                        await processor.Invoke(cmd);
                    }
                }
               
                return result;
            } finally  {
                if (!input.TransactionMode && conn is DbConnection dbc) {
                    await dbc.CloseAsync();
                    await dbc.DisposeAsync(); //Dispose immediately to return to the pool
                    input.Logger?.LogInformation("Connection closed");
                }
            }
        }

        public async Task<object> NonQuery(IAdapterArgs input, params (string key, object value)[] parameters) {

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

        public async Task<object> Read(IAdapterArgs input, params (string key, object value)[] parameters) {
            var result = await ExecuteInternal(input, async (dbc) => {
                if (!(dbc is DbCommand cmd)) return null;
                if (input.Prepare) {
                    await cmd.PrepareAsync();
                }
                DataSet ds = new DataSet();

                using (var reader = await cmd.ExecuteReaderAsync()) {
                    int count = 1;
                    //if (reader == null) return null;
                    //Don't load the first one directly. It will not capture other results.
                    do {
                        //await reader.ReadAsync();

                        //Read all tables and return the final one.
                        //DataTable dt = new DataTable();
                        //dt.Load(reader); //Load normalized Reader
                        DataTable dt = LoadNormalizedTable(reader); //Load normalized Reader
                        ds.Tables.Add(dt);
                        input.Logger?.LogInformation($@"For query {input.Query} - : Table Count - {count} created.");
                        count++;
                    } while (reader.NextResult());
                }
                return ds; //Only return dataset
            }, parameters);

            return result as DataSet;
        }

        public DataTable LoadNormalizedTable(IDataReader reader) {
            var table = new DataTable();

            for (int i = 0; i < reader.FieldCount; i++) {
                var columnType = reader.GetFieldType(i);
                var columnName = reader.GetName(i);

                // Normalize PostgreSQL bit(n) to string for readability
                if (columnType == typeof(BitArray) &&
                    reader.GetDataTypeName(i).StartsWith("bit", StringComparison.OrdinalIgnoreCase)) {
                    table.Columns.Add(columnName, typeof(string));
                } else {
                    table.Columns.Add(columnName, columnType);
                }
            }

            while (reader.Read()) {
                var row = table.NewRow();
                for (int i = 0; i < reader.FieldCount; i++) {
                    var value = reader.GetValue(i);

                    if (value is BitArray ba) {
                        // Convert BitArray to string like "101"
                        var bits = new char[ba.Length];
                        for (int b = 0; b < ba.Length; b++) {
                            bits[b] = ba[b] ? '1' : '0';
                        }

                        row[i] = new string(bits);
                    } else {
                        row[i] = value is DBNull ? DBNull.Value : value;
                    }
                }
                table.Rows.Add(row);
            }

            return table;
        }



        public async Task<object> Scalar(IAdapterArgs input, params (string key, object value)[] parameters) {
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

        public IDBTransaction Begin(bool ExcludeDBInConnectionString = false) {
            if (_transaction != null) throw new Exception("A transaction is already opened. Please commit/rollback the existing transaction.");
            var constring = _conString;
            if (ExcludeDBInConnectionString) {
                //Which means we are trying to do something without the database information.
                //Now, in this we need to remove the database information. Because we are only trying to run the operation at the connection level. May be we are trying to create the database here.
                constring = _conString.RemoveKeys(';', "database");
            }
            _connection = (DbConnection)GetConnection(constring, true);
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
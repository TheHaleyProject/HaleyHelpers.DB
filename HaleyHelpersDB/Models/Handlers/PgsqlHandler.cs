using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text.RegularExpressions;
using Haley.Utils;
using Haley.Enums;
using Haley.Abstractions;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using NpgsqlTypes;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Diagnostics;
using Haley.Internal;
using static Haley.Internal.QueryFields;

namespace Haley.Models {

    internal class PgsqlHandler : SqlHandlerBase {
        static ConcurrentDictionary<string, NpgsqlDataSource> _dataSources = new ConcurrentDictionary<string, NpgsqlDataSource>();
        protected override string ProviderName { get; } = "PGSQL";
        public PgsqlHandler(ConInfo conInfo) : base(conInfo) { }
        //NpgsqlDataSource.Create(input.Conn)
        protected override IDbCommand CreateWrappedCommand(object conn) {
            if (conn is NpgsqlDataSource npgs) return npgs.CreateCommand();
            //return base.GetCommand(conn); //this might return stack overflow
            throw new NotImplementedException();
        }

        protected override bool IsConnectionWrapped(object conn) {
            if (conn is NpgsqlDataSource npgs) return true;
            return false;
        }

        protected override void FillParameterInternal(IDbDataParameter msp, object pvalue) {
            if (msp is NpgsqlParameter npsp) {
                var tup = (ITuple)pvalue;
                msp.Value = tup[0] ?? DBNull.Value;
                if (tup.Length > 1 && tup[1] is NpgsqlDbType dbt) npsp.NpgsqlDbType = dbt;
            } else {
                throw new NotImplementedException();
            }
        }
        protected override object GetConnection(ConInfo conInfo, bool forTransaction) {
            //if (TransactionMode) return NpgsqlDataSource.Create(conStr).CreateConnection();
            if (_transaction != null) return _connection; //use the same connection 
            //if (forTransaction) {
               
            //}

            if (!_dataSources.ContainsKey(conInfo.ConString)) _dataSources.TryAdd(conInfo.ConString, NpgsqlDataSource.Create(conInfo.ConString));
            return _dataSources[conInfo.ConString].CreateConnection();
        }

        protected override IDbDataParameter GetParameter() {
            return new NpgsqlParameter();
        }

        internal override async Task<DatabaseBootstrapOutcome> BootstrapDatabaseAsync(
            string databaseName,
            string sql,
            DatabaseBootstrapArgs args,
            CancellationToken cancellationToken) {
            var maintenanceDatabase = string.IsNullOrWhiteSpace(args.PostgreSqlMaintenanceDatabase)
                ? "postgres"
                : args.PostgreSqlMaintenanceDatabase;
            var maintenanceInfo = GetDatabaseConInfo(maintenanceDatabase, disablePooling: true);
            await using var maintenance = await OpenConnectionAsync(maintenanceInfo, cancellationToken).ConfigureAwait(false);
            var lockKey = args.LockKey ?? $"haley-bootstrap:{databaseName}";
            await AcquireBootstrapLockAsync(
                maintenance,
                lockKey,
                args.LockTimeoutSeconds,
                cancellationToken).ConfigureAwait(false);

            var created = false;
            try {
                var exists = await ExecuteScalarOnConnectionAsync(
                    maintenance,
                    QRY_PGSQL.DATABASE_EXISTS,
                    null,
                    cancellationToken,
                    (DATABASE_NAME, databaseName)).ConfigureAwait(false) is not null;
                if (!exists) {
                    RequireCreation(args, databaseName);
                    try {
                        await ExecuteNonQueryOnConnectionAsync(
                            maintenance,
                            QRY_PGSQL.CreateDatabase(QuoteIdentifier(databaseName)),
                            null,
                            cancellationToken).ConfigureAwait(false);
                        created = true;
                    } catch (PostgresException exception) when (exception.SqlState == "42P04") {
                        // A creator not using Haley may have won the race.
                    }
                }

                try {
                    var targetInfo = GetDatabaseConInfo(databaseName, disablePooling: true);
                    await using var target = await OpenConnectionAsync(targetInfo, cancellationToken).ConfigureAwait(false);
                    await using var transaction = await target.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                    try {
                        await ExecuteNonQueryOnConnectionAsync(
                            target,
                            sql,
                            transaction,
                            cancellationToken).ConfigureAwait(false);
                        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    } catch {
                        await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                        throw;
                    }
                    return new DatabaseBootstrapOutcome(created);
                } catch {
                    if (created && HasFlag(args, DatabaseBootstrapFlags.DropNewDatabaseOnFailure)) {
                        await ExecuteNonQueryOnConnectionAsync(
                            maintenance,
                            QRY_PGSQL.DropDatabase(QuoteIdentifier(databaseName)),
                            null,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    throw;
                }
            } finally {
                if (maintenance.State == ConnectionState.Open) {
                    try {
                        await ExecuteScalarOnConnectionAsync(
                            maintenance,
                            QRY_PGSQL.RELEASE_BOOTSTRAP_LOCK,
                            null,
                            CancellationToken.None,
                            (LOCK_KEY, lockKey)).ConfigureAwait(false);
                    } catch {
                        // Closing the maintenance connection releases the advisory lock.
                    }
                }
            }
        }

        private async Task AcquireBootstrapLockAsync(
            DbConnection connection,
            string lockKey,
            int timeoutSeconds,
            CancellationToken cancellationToken) {
            var timeout = TimeSpan.FromSeconds(Math.Max(0, timeoutSeconds));
            var watch = Stopwatch.StartNew();
            do {
                var result = await ExecuteScalarOnConnectionAsync(
                    connection,
                    QRY_PGSQL.ACQUIRE_BOOTSTRAP_LOCK,
                    null,
                    cancellationToken,
                    (LOCK_KEY, lockKey)).ConfigureAwait(false);
                if (result is bool acquired && acquired) return;
                if (watch.Elapsed >= timeout) break;
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            } while (true);
            throw new TimeoutException($"Timed out while waiting for PostgreSQL bootstrap lock '{lockKey}'.");
        }

        private static void RequireCreation(DatabaseBootstrapArgs args, string databaseName) {
            if (!HasFlag(args, DatabaseBootstrapFlags.CreateDatabaseIfMissing))
                throw new InvalidOperationException(
                    $"Database '{databaseName}' does not exist and physical database creation is disabled.");
        }

        private static bool HasFlag(DatabaseBootstrapArgs args, DatabaseBootstrapFlags flag) =>
            (args.Flags & flag) == flag;

        private static string QuoteIdentifier(string value) =>
            $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}

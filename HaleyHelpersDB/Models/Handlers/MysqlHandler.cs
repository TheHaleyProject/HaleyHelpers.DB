using Haley.Abstractions;
using Microsoft.Data.SqlClient;
using Haley.Utils;
using Haley.Enums;
using Microsoft.Extensions.Logging;
//using MySql.Data.MySqlClient;
using MySqlConnector;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Haley.Internal;
using static Haley.Internal.QueryFields;

namespace Haley.Models {

    internal class MysqlHandler : SqlHandlerBase {
        protected override string ProviderName { get; } = "MYSQL";

        protected override object GetConnection(ConInfo conInfo, bool forTransaction) {
            if (_transaction != null) return _connection; //use the same connection 
            if (conInfo.IgnoreSsl == true) {
                var builder = new MySqlConnectionStringBuilder(conInfo.ConString) {
                    SslMode = MySqlSslMode.None
                };
                return new MySqlConnection(builder.ConnectionString);
            }
            return new MySqlConnection(conInfo.ConString);
        }

        protected override IDbDataParameter GetParameter() {
            return new MySqlParameter();
        }

        internal override async Task<DatabaseBootstrapOutcome> BootstrapDatabaseAsync(
            string databaseName,
            string sql,
            DatabaseBootstrapArgs args,
            CancellationToken cancellationToken) {
            var maintenanceInfo = GetDatabaseConInfo(null, disablePooling: true);
            await using var maintenance = await OpenConnectionAsync(maintenanceInfo, cancellationToken).ConfigureAwait(false);
            var lockKey = args.LockKey ?? $"haley-bootstrap:{databaseName}";
            var lockResult = await ExecuteScalarOnConnectionAsync(
                maintenance,
                QRY_MARIA.ACQUIRE_BOOTSTRAP_LOCK,
                null,
                cancellationToken,
                (LOCK_KEY, lockKey),
                (LOCK_TIMEOUT, Math.Max(0, args.LockTimeoutSeconds))).ConfigureAwait(false);
            var lockAcquired = Convert.ToInt32(lockResult, CultureInfo.InvariantCulture) == 1;
            if (!lockAcquired)
                throw new TimeoutException($"Timed out while waiting for MariaDB bootstrap lock '{lockKey}'.");

            var created = false;
            try {
                var exists = await ExecuteScalarOnConnectionAsync(
                    maintenance,
                    QRY_MARIA.DATABASE_EXISTS,
                    null,
                    cancellationToken,
                    (DATABASE_NAME, databaseName)).ConfigureAwait(false) is not null;
                if (!exists) {
                    RequireCreation(args, databaseName);
                    await ExecuteNonQueryOnConnectionAsync(
                        maintenance,
                        QRY_MARIA.CreateDatabase(QuoteIdentifier(databaseName)),
                        null,
                        cancellationToken).ConfigureAwait(false);
                    created = true;
                }

                try {
                    var targetInfo = GetDatabaseConInfo(databaseName, disablePooling: true);
                    await using var target = await OpenConnectionAsync(targetInfo, cancellationToken).ConfigureAwait(false);
                    foreach (var statement in MariaSqlScript.SplitStatements(sql)) {
                        await ExecuteNonQueryOnConnectionAsync(
                            target,
                            statement,
                            null,
                            cancellationToken).ConfigureAwait(false);
                    }
                    return new DatabaseBootstrapOutcome(created);
                } catch {
                    if (created && HasFlag(args, DatabaseBootstrapFlags.DropNewDatabaseOnFailure)) {
                        await ExecuteNonQueryOnConnectionAsync(
                            maintenance,
                            QRY_MARIA.DropDatabase(QuoteIdentifier(databaseName)),
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
                            QRY_MARIA.RELEASE_BOOTSTRAP_LOCK,
                            null,
                            CancellationToken.None,
                            (LOCK_KEY, lockKey)).ConfigureAwait(false);
                    } catch {
                        // Closing the maintenance connection releases the named lock.
                    }
                }
            }
        }

        private static void RequireCreation(DatabaseBootstrapArgs args, string databaseName) {
            if (!HasFlag(args, DatabaseBootstrapFlags.CreateDatabaseIfMissing))
                throw new InvalidOperationException(
                    $"Database '{databaseName}' does not exist and physical database creation is disabled.");
        }

        private static bool HasFlag(DatabaseBootstrapArgs args, DatabaseBootstrapFlags flag) =>
            (args.Flags & flag) == flag;

        private static string QuoteIdentifier(string value) =>
            $"`{value.Replace("`", "``", StringComparison.Ordinal)}`";

        public MysqlHandler(ConInfo conInfo) : base(conInfo) { }
    }
}

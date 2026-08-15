using System.Net;
using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using MySqlConnector;
using Npgsql;

namespace Haley.Utils;

public static class DatabaseBootstrapExtensions
{
    private const string FeedbackSource = "HALEY-DB-BOOTSTRAP";
    private const string PostgreSqlMissingDatabaseState = "3D000";
    private const string PostgreSqlDuplicateDatabaseState = "42P04";

    public static async Task<IFeedback> BootstrapDatabaseAsync(
        this IAdapterGateway gateway,
        DatabaseBootstrapArgs args,
        CancellationToken cancellationToken = default)
    {
        var feedback = new Feedback().SetSource(FeedbackSource);
        try
        {
            ArgumentNullException.ThrowIfNull(gateway);
            ArgumentNullException.ThrowIfNull(args);
            cancellationToken.ThrowIfCancellationRequested();

            EnsureAdapter(gateway, args);
            var adapter = gateway[args.AdapterKey];
            var info = adapter.Info ?? throw new InvalidOperationException(
                $"Adapter information is missing for '{args.AdapterKey}'.");
            var connectionString = info.ConnectionInfo?.ConString;
            if (string.IsNullOrWhiteSpace(connectionString)) connectionString = info.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException($"Adapter '{args.AdapterKey}' has no connection string.");

            var databaseName = ResolveDatabaseName(info, args);
            var sql = LoadSql(args, databaseName);
            var created = info.DBType switch
            {
                TargetDB.maria or TargetDB.mysql => await BootstrapMariaAsync(
                    connectionString, databaseName, sql, args, cancellationToken).ConfigureAwait(false),
                TargetDB.pgsql => await BootstrapPostgreSqlAsync(
                    connectionString, databaseName, sql, args, cancellationToken).ConfigureAwait(false),
                _ => throw new NotSupportedException(
                    $"Database bootstrap is not supported for target '{info.DBType}'.")
            };

            return feedback
                .SetStatus(true)
                .SetCode((int)(created ? HttpStatusCode.Created : HttpStatusCode.OK))
                .SetMessage(created
                    ? $"Database '{databaseName}' was created and its bootstrap SQL was applied."
                    : $"Database '{databaseName}' already existed and its bootstrap SQL was reapplied.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return feedback
                .SetCode((int)HttpStatusCode.InternalServerError)
                .SetMessage(exception.Message)
                .SetTrace(exception.ToString());
        }
    }

    private static void EnsureAdapter(IAdapterGateway gateway, DatabaseBootstrapArgs args)
    {
        if (gateway.ContainsKey(args.AdapterKey)) return;
        if (string.IsNullOrWhiteSpace(args.CloningAdapterKey))
            throw new InvalidOperationException($"Adapter '{args.AdapterKey}' is not registered.");
        if (!gateway.ContainsKey(args.CloningAdapterKey))
            throw new InvalidOperationException($"Cloning adapter '{args.CloningAdapterKey}' is not registered.");

        var databaseName = FirstValue(args.DatabaseName, args.FallbackDatabaseName)
            ?? throw new InvalidOperationException(
                "DatabaseName or FallbackDatabaseName is required when cloning an adapter.");
        var duplicate = gateway.DuplicateAdapter(
            args.CloningAdapterKey,
            args.AdapterKey,
            ("database", databaseName));
        if (duplicate is null || !duplicate.Status)
            throw new InvalidOperationException(
                duplicate?.Message ?? $"Unable to clone adapter '{args.CloningAdapterKey}'.");
    }

    private static string ResolveDatabaseName(IAdapterConfig info, DatabaseBootstrapArgs args)
    {
        var configuredName = FirstValue(
            info.DBName,
            ConnectionStringValue(info.ConnectionString, "database"),
            ConnectionStringValue(info.ConnectionInfo?.ConString, "database"));
        var requestedName = FirstValue(args.DatabaseName, configuredName, args.FallbackDatabaseName);
        if (string.IsNullOrWhiteSpace(requestedName))
            throw new InvalidOperationException(
                $"Adapter '{args.AdapterKey}' does not define a database name.");
        if (!string.IsNullOrWhiteSpace(configuredName) &&
            !string.Equals(configuredName, requestedName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Bootstrap database '{requestedName}' does not match adapter database '{configuredName}'.");
        return requestedName;
    }

    private static string LoadSql(DatabaseBootstrapArgs args, string databaseName)
    {
        var content = args.SqlContent;
        if (string.IsNullOrWhiteSpace(content))
        {
            if (string.IsNullOrWhiteSpace(args.SqlPath))
                throw new InvalidOperationException("SqlContent or SqlPath is required.");
            if (!File.Exists(args.SqlPath))
                throw new FileNotFoundException("Database bootstrap SQL was not found.", args.SqlPath);
            content = File.ReadAllText(args.SqlPath);
        }

        foreach (var replacement in args.VariablesToReplace)
        {
            if (!string.IsNullOrWhiteSpace(replacement.Key))
                content = content.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
        }

        if (args.ContentProcessor is not null)
            content = args.ContentProcessor(content, databaseName);
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Database bootstrap SQL is empty after processing.");
        return content;
    }

    private static async Task<bool> BootstrapMariaAsync(
        string connectionString,
        string databaseName,
        string sql,
        DatabaseBootstrapArgs args,
        CancellationToken cancellationToken)
    {
        var targetBuilder = new MySqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName,
            Pooling = false
        };
        var maintenanceBuilder = new MySqlConnectionStringBuilder(connectionString)
        {
            Database = string.Empty,
            Pooling = false
        };

        var created = false;
        await using (var maintenance = new MySqlConnection(maintenanceBuilder.ConnectionString))
        {
            await maintenance.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var existsCommand = new MySqlCommand(
                "SELECT 1 FROM information_schema.schemata WHERE schema_name = @database_name LIMIT 1;",
                maintenance);
            existsCommand.Parameters.AddWithValue("@database_name", databaseName);
            var exists = await existsCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
            if (!exists)
            {
                RequireCreationPermission(args, databaseName);
                await using var createCommand = new MySqlCommand(
                    $"CREATE DATABASE IF NOT EXISTS {QuoteMariaIdentifier(databaseName)} " +
                    "CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;",
                    maintenance);
                await createCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                created = true;
            }
        }

        try
        {
            await ApplyMariaSqlAsync(
                targetBuilder.ConnectionString,
                databaseName,
                sql,
                args,
                cancellationToken).ConfigureAwait(false);
            return created;
        }
        catch
        {
            if (created && HasFlag(args, DatabaseBootstrapFlags.DropNewDatabaseOnFailure))
                await DropMariaDatabaseAsync(
                    maintenanceBuilder.ConnectionString,
                    databaseName,
                    cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ApplyMariaSqlAsync(
        string connectionString,
        string databaseName,
        string sql,
        DatabaseBootstrapArgs args,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var lockKey = args.LockKey ?? $"haley-bootstrap:{databaseName}:{args.AdapterKey}";
        var lockAcquired = false;
        try
        {
            await using (var lockCommand = new MySqlCommand(
                "SELECT GET_LOCK(@lock_key, @lock_timeout);",
                connection))
            {
                lockCommand.Parameters.AddWithValue("@lock_key", lockKey);
                lockCommand.Parameters.AddWithValue("@lock_timeout", Math.Max(0, args.LockTimeoutSeconds));
                var result = await lockCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                lockAcquired = Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) == 1;
            }
            if (!lockAcquired)
                throw new TimeoutException($"Timed out while waiting for MariaDB bootstrap lock '{lockKey}'.");

            foreach (var statement in MariaSqlScript.SplitStatements(sql))
            {
                await using var command = new MySqlCommand(statement, connection);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (lockAcquired && connection.State == System.Data.ConnectionState.Open)
            {
                await using var releaseCommand = new MySqlCommand("SELECT RELEASE_LOCK(@lock_key);", connection);
                releaseCommand.Parameters.AddWithValue("@lock_key", lockKey);
                await releaseCommand.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static async Task<bool> BootstrapPostgreSqlAsync(
        string connectionString,
        string databaseName,
        string sql,
        DatabaseBootstrapArgs args,
        CancellationToken cancellationToken)
    {
        var targetBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName,
            Pooling = false
        };
        var created = !await PostgreSqlDatabaseExistsAsync(
            targetBuilder.ConnectionString,
            cancellationToken).ConfigureAwait(false);
        var maintenanceBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = string.IsNullOrWhiteSpace(args.PostgreSqlMaintenanceDatabase)
                ? "postgres"
                : args.PostgreSqlMaintenanceDatabase,
            Pooling = false
        };

        if (created)
        {
            RequireCreationPermission(args, databaseName);
            await CreatePostgreSqlDatabaseAsync(
                maintenanceBuilder.ConnectionString,
                databaseName,
                cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await ApplyPostgreSqlSqlAsync(
                targetBuilder.ConnectionString,
                databaseName,
                sql,
                args,
                cancellationToken).ConfigureAwait(false);
            return created;
        }
        catch
        {
            if (created && HasFlag(args, DatabaseBootstrapFlags.DropNewDatabaseOnFailure))
                await DropPostgreSqlDatabaseAsync(
                    maintenanceBuilder.ConnectionString,
                    databaseName,
                    cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<bool> PostgreSqlDatabaseExistsAsync(
        string targetConnectionString,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(targetConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgreSqlMissingDatabaseState)
        {
            return false;
        }
    }

    private static async Task CreatePostgreSqlDatabaseAsync(
        string maintenanceConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(maintenanceConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = new NpgsqlCommand(
                $"CREATE DATABASE {QuotePostgreSqlIdentifier(databaseName)};",
                connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgreSqlDuplicateDatabaseState)
        {
            // Another host created the same database after our existence check.
        }
    }

    private static async Task ApplyPostgreSqlSqlAsync(
        string connectionString,
        string databaseName,
        string sql,
        DatabaseBootstrapArgs args,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var lockKey = args.LockKey ?? $"haley-bootstrap:{databaseName}:{args.AdapterKey}";
            await using (var lockCommand = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended(@lock_key, 0));",
                connection,
                transaction))
            {
                lockCommand.Parameters.AddWithValue("lock_key", lockKey);
                await lockCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task DropMariaDatabaseAsync(
        string maintenanceConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(maintenanceConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new MySqlCommand(
            $"DROP DATABASE IF EXISTS {QuoteMariaIdentifier(databaseName)};",
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DropPostgreSqlDatabaseAsync(
        string maintenanceConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(maintenanceConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS {QuotePostgreSqlIdentifier(databaseName)};",
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void RequireCreationPermission(DatabaseBootstrapArgs args, string databaseName)
    {
        if (!HasFlag(args, DatabaseBootstrapFlags.CreateDatabaseIfMissing))
            throw new InvalidOperationException(
                $"Database '{databaseName}' does not exist and physical database creation is disabled.");
    }

    private static bool HasFlag(DatabaseBootstrapArgs args, DatabaseBootstrapFlags flag) =>
        (args.Flags & flag) == flag;

    private static string QuoteMariaIdentifier(string value) => $"`{value.Replace("`", "``", StringComparison.Ordinal)}`";
    private static string QuotePostgreSqlIdentifier(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string? ConnectionStringValue(string? connectionString, string key)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        foreach (var pair in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0) continue;
            if (string.Equals(pair[..separator].Trim(), key, StringComparison.OrdinalIgnoreCase))
                return pair[(separator + 1)..].Trim();
        }
        return null;
    }

    private static string? FirstValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}

public static class MariaSqlScript
{
    public static IReadOnlyList<string> SplitStatements(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return Array.Empty<string>();

        var statements = new List<string>();
        var buffer = new System.Text.StringBuilder(content.Length);
        var delimiter = ";";
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBacktick = false;
        var inLineComment = false;
        var inBlockComment = false;
        var lineStart = true;

        for (var index = 0; index < content.Length; index++)
        {
            if (lineStart && !inSingleQuote && !inDoubleQuote && !inBacktick && !inBlockComment)
            {
                var directiveIndex = index;
                while (directiveIndex < content.Length &&
                       content[directiveIndex] is ' ' or '\t' or '\r') directiveIndex++;
                const string directive = "DELIMITER";
                if (directiveIndex + directive.Length <= content.Length &&
                    string.Equals(
                        content.Substring(directiveIndex, directive.Length),
                        directive,
                        StringComparison.OrdinalIgnoreCase))
                {
                    var valueStart = directiveIndex + directive.Length;
                    while (valueStart < content.Length && char.IsWhiteSpace(content[valueStart]) &&
                           content[valueStart] != '\n') valueStart++;
                    var valueEnd = valueStart;
                    while (valueEnd < content.Length && content[valueEnd] is not '\r' and not '\n') valueEnd++;
                    delimiter = content[valueStart..valueEnd].Trim();
                    if (string.IsNullOrWhiteSpace(delimiter))
                        throw new FormatException("A MariaDB DELIMITER directive has no delimiter value.");
                    index = valueEnd - 1;
                    lineStart = true;
                    continue;
                }
            }

            var current = content[index];
            var next = index + 1 < content.Length ? content[index + 1] : '\0';

            if (inLineComment)
            {
                buffer.Append(current);
                if (current == '\n')
                {
                    inLineComment = false;
                    lineStart = true;
                }
                continue;
            }
            if (inBlockComment)
            {
                buffer.Append(current);
                if (current == '*' && next == '/')
                {
                    buffer.Append(next);
                    index++;
                    inBlockComment = false;
                }
                lineStart = current == '\n';
                continue;
            }
            if (inSingleQuote || inDoubleQuote || inBacktick)
            {
                buffer.Append(current);
                if (current == '\\' && next != '\0')
                {
                    buffer.Append(next);
                    index++;
                    continue;
                }
                if (inSingleQuote && current == '\'' || inDoubleQuote && current == '"' || inBacktick && current == '`')
                {
                    if (next == current)
                    {
                        buffer.Append(next);
                        index++;
                    }
                    else
                    {
                        inSingleQuote = inDoubleQuote = inBacktick = false;
                    }
                }
                lineStart = current == '\n';
                continue;
            }

            if (current == '-' && next == '-' && StartsSqlLineComment(content, index + 2) || current == '#')
            {
                buffer.Append(current);
                if (current == '-')
                {
                    buffer.Append(next);
                    index++;
                }
                inLineComment = true;
                lineStart = false;
                continue;
            }
            if (current == '/' && next == '*')
            {
                buffer.Append(current).Append(next);
                index++;
                inBlockComment = true;
                lineStart = false;
                continue;
            }
            if (current == '\'' || current == '"' || current == '`')
            {
                buffer.Append(current);
                inSingleQuote = current == '\'';
                inDoubleQuote = current == '"';
                inBacktick = current == '`';
                lineStart = false;
                continue;
            }
            if (Matches(content, index, delimiter))
            {
                AddStatement(statements, buffer);
                index += delimiter.Length - 1;
                lineStart = false;
                continue;
            }

            buffer.Append(current);
            lineStart = current == '\n';
        }

        AddStatement(statements, buffer);
        return statements;
    }

    private static void AddStatement(List<string> statements, System.Text.StringBuilder buffer)
    {
        var statement = buffer.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(statement)) statements.Add(statement);
        buffer.Clear();
    }

    private static bool Matches(string content, int index, string delimiter) =>
        index + delimiter.Length <= content.Length &&
        string.CompareOrdinal(content, index, delimiter, 0, delimiter.Length) == 0;

    private static bool StartsSqlLineComment(string content, int index) =>
        index >= content.Length || content[index] is ' ' or '\t' or '\r' or '\n';
}

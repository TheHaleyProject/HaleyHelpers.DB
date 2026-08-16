using System.Net;
using System.Text;
using Haley.Abstractions;
using Haley.Models;

namespace Haley.Utils;

public static class DatabaseBootstrapExtensions
{
    private const string FeedbackSource = "HALEY-DB-BOOTSTRAP";

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
            if (adapter is not DBAdapter haleyAdapter)
                throw new NotSupportedException(
                    $"Adapter '{args.AdapterKey}' is not backed by Haley's database handlers.");

            var databaseName = ResolveDatabaseName(adapter.Info, args);
            var sql = LoadSql(args, databaseName);
            var outcome = await haleyAdapter.BootstrapDatabaseAsync(
                databaseName,
                sql,
                args,
                cancellationToken).ConfigureAwait(false);

            return feedback
                .SetStatus(true)
                .SetCode((int)(outcome.DatabaseCreated ? HttpStatusCode.Created : HttpStatusCode.OK))
                .SetMessage(outcome.DatabaseCreated
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
        if (info is null)
            throw new InvalidOperationException($"Adapter information is missing for '{args.AdapterKey}'.");
        var configuredName = FirstValue(
            info.DBName,
            Convert.ToString(info.ConnectionString?.GetValue("database")),
            Convert.ToString(info.ConnectionInfo?.ConString?.GetValue("database")));
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

    private static string? FirstValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}

public static class MariaSqlScript
{
    public static IReadOnlyList<string> SplitStatements(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return Array.Empty<string>();

        var statements = new List<string>();
        var buffer = new StringBuilder(content.Length);
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

    private static void AddStatement(List<string> statements, StringBuilder buffer)
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

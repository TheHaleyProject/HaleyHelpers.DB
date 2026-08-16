using Haley.Enums;

namespace Haley.Models;

public sealed class DatabaseBootstrapArgs
{
    public DatabaseBootstrapArgs(string adapterKey)
    {
        AdapterKey = string.IsNullOrWhiteSpace(adapterKey)
            ? throw new ArgumentException("An adapter key is required.", nameof(adapterKey))
            : adapterKey;
    }

    public string AdapterKey { get; }
    public string? DatabaseName { get; set; }
    public string? FallbackDatabaseName { get; set; }
    public string? SqlPath { get; set; }
    public string? SqlContent { get; set; }
    public string? CloningAdapterKey { get; set; }
    public string PostgreSqlMaintenanceDatabase { get; set; } = "postgres";
    public string? LockKey { get; set; }
    public int LockTimeoutSeconds { get; set; } = 60;
    public DatabaseBootstrapFlags Flags { get; set; } =
        DatabaseBootstrapFlags.CreateDatabaseIfMissing |
        DatabaseBootstrapFlags.DropNewDatabaseOnFailure;
    public Dictionary<string, string> VariablesToReplace { get; set; } = new(StringComparer.Ordinal);
    public Func<string, string, string>? ContentProcessor { get; set; }
}

internal readonly record struct DatabaseBootstrapOutcome(bool DatabaseCreated);

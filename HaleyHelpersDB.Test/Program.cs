using Haley.Enums;
using Haley.Models;
using Haley.Utils;

RunParserChecks();

if (args.Contains("--integration", StringComparer.OrdinalIgnoreCase))
{
    var maria = Environment.GetEnvironmentVariable("HALEY_TEST_MARIA");
    var postgres = Environment.GetEnvironmentVariable("HALEY_TEST_POSTGRES");
    if (string.IsNullOrWhiteSpace(maria) || string.IsNullOrWhiteSpace(postgres))
        throw new InvalidOperationException(
            "Set HALEY_TEST_MARIA and HALEY_TEST_POSTGRES before running integration checks.");

    await VerifyMariaBootstrapAsync(maria);
    await VerifyPostgreSqlBootstrapAsync(postgres);
}

Console.WriteLine("Haley database bootstrap checks passed.");

static void RunParserChecks()
{
    const string routine = """
        DELIMITER //
        CREATE PROCEDURE bootstrap_test()
        BEGIN
            SELECT 'value;inside';
            SELECT 2;
        END//
        DELIMITER ;
        CREATE TABLE IF NOT EXISTS bootstrap_probe (id BIGINT PRIMARY KEY);
        """;
    var routineStatements = MariaSqlScript.SplitStatements(routine);
    Assert(routineStatements.Count == 2, "DELIMITER routine should produce two statements.");
    Assert(routineStatements[0].Contains("SELECT 2;", StringComparison.Ordinal),
        "Routine-internal semicolons must remain intact.");

    const string commentsAndQuotes = """
        -- a comment containing ;
        CREATE TABLE IF NOT EXISTS alpha (text_value VARCHAR(50) DEFAULT 'a;b');
        /* a block comment containing ; */
        CREATE TABLE IF NOT EXISTS beta (id BIGINT PRIMARY KEY);
        """;
    var ordinaryStatements = MariaSqlScript.SplitStatements(commentsAndQuotes);
    Assert(ordinaryStatements.Count == 2, "Comments and quoted semicolons must not split statements.");

    var defaults = new DatabaseBootstrapArgs("test").Flags;
    Assert((defaults & DatabaseBootstrapFlags.CreateDatabaseIfMissing) != 0,
        "Database creation must be enabled by default.");
    Assert((defaults & DatabaseBootstrapFlags.DropNewDatabaseOnFailure) != 0,
        "Cleanup of a newly-created failed database must be enabled by default.");
}

static async Task VerifyMariaBootstrapAsync(string connectionString)
{
    var databaseName = NewDatabaseName("haley_bootstrap_maria");
    var failedDatabaseName = NewDatabaseName("haley_bootstrap_maria");
    var gateway = CreateGateway(connectionString, TargetDB.maria, databaseName, "maria-test");
    try
    {
        const string firstCapabilitySql = """
            CREATE TABLE IF NOT EXISTS bootstrap_probe (
                id BIGINT NOT NULL PRIMARY KEY,
                value_text VARCHAR(100) NOT NULL
            );
            """;
        var first = await gateway.BootstrapDatabaseAsync(
            new DatabaseBootstrapArgs("maria-test") { SqlContent = firstCapabilitySql });
        Assert(first.Status && first.Code == 201, $"MariaDB first bootstrap failed: {first.Message}");

        await gateway.ExecAsync("maria-test", "DROP TABLE bootstrap_probe;");
        var replay = await gateway.BootstrapDatabaseAsync(
            new DatabaseBootstrapArgs("maria-test") { SqlContent = firstCapabilitySql });
        Assert(replay.Status && replay.Code == 200, $"MariaDB replay failed: {replay.Message}");

        gateway.Add(CreateAdapter(connectionString, TargetDB.maria, databaseName, "maria-second"));
        var second = await gateway.BootstrapDatabaseAsync(new DatabaseBootstrapArgs("maria-second")
        {
            SqlContent = "CREATE TABLE IF NOT EXISTS second_capability_probe (id BIGINT NOT NULL PRIMARY KEY);"
        });
        Assert(second.Status && second.Code == 200, $"MariaDB shared-database bootstrap failed: {second.Message}");

        var tableCount = await gateway.ScalarAsync<long>(
            "maria-test",
            "SELECT COUNT(*) FROM information_schema.tables " +
            "WHERE table_schema = @database_name AND table_name IN ('bootstrap_probe', 'second_capability_probe');",
            default,
            ("database_name", databaseName));
        Assert(tableCount == 2, "MariaDB replay/shared database did not create both capability tables.");

        var existingFailure = await gateway.BootstrapDatabaseAsync(
            new DatabaseBootstrapArgs("maria-test") { SqlContent = "THIS IS NOT VALID SQL;" });
        Assert(!existingFailure.Status, "MariaDB invalid SQL should report failure.");
        Assert(await MariaDatabaseExistsAsync(connectionString, databaseName),
            "MariaDB bootstrap failure must not remove a pre-existing database.");

        var failedGateway = CreateGateway(
            connectionString,
            TargetDB.maria,
            failedDatabaseName,
            "maria-failed-test");
        var newDatabaseFailure = await failedGateway.BootstrapDatabaseAsync(
            new DatabaseBootstrapArgs("maria-failed-test") { SqlContent = "THIS IS NOT VALID SQL;" });
        Assert(!newDatabaseFailure.Status, "MariaDB failed-new-database test should report failure.");
        Assert(!await MariaDatabaseExistsAsync(connectionString, failedDatabaseName),
            "MariaDB should remove only the database created by the failed bootstrap call.");
    }
    finally
    {
        await DropMariaDatabaseAsync(connectionString, databaseName);
        await DropMariaDatabaseAsync(connectionString, failedDatabaseName);
    }
}

static async Task VerifyPostgreSqlBootstrapAsync(string connectionString)
{
    var databaseName = NewDatabaseName("haley_bootstrap_pg");
    var failedDatabaseName = NewDatabaseName("haley_bootstrap_pg");
    var gateway = CreateGateway(connectionString, TargetDB.pgsql, databaseName, "pg-test");
    try
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS bootstrap_probe (
                id bigint PRIMARY KEY,
                value_text text NOT NULL
            );
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM bootstrap_probe WHERE id = 1) THEN
                    INSERT INTO bootstrap_probe (id, value_text) VALUES (1, 'created');
                END IF;
            END
            $$;
            """;
        var first = await gateway.BootstrapDatabaseAsync(
            new DatabaseBootstrapArgs("pg-test") { SqlContent = sql });
        Assert(first.Status && first.Code == 201, $"PostgreSQL first bootstrap failed: {first.Message}");

        await gateway.ExecAsync("pg-test", "DROP TABLE bootstrap_probe;");
        var replay = await gateway.BootstrapDatabaseAsync(
            new DatabaseBootstrapArgs("pg-test") { SqlContent = sql });
        Assert(replay.Status && replay.Code == 200, $"PostgreSQL replay failed: {replay.Message}");

        var rowCount = await gateway.ScalarAsync<long>(
            "pg-test",
            "SELECT COUNT(*) FROM bootstrap_probe WHERE id = 1;");
        Assert(rowCount == 1, "PostgreSQL did not execute the complete DO block during replay.");

        var existingFailure = await gateway.BootstrapDatabaseAsync(
            new DatabaseBootstrapArgs("pg-test") { SqlContent = "THIS IS NOT VALID SQL;" });
        Assert(!existingFailure.Status, "PostgreSQL invalid SQL should report failure.");
        Assert(await PostgreSqlDatabaseExistsAsync(connectionString, databaseName),
            "PostgreSQL bootstrap failure must not remove a pre-existing database.");

        var failedGateway = CreateGateway(
            connectionString,
            TargetDB.pgsql,
            failedDatabaseName,
            "pg-failed-test");
        var newDatabaseFailure = await failedGateway.BootstrapDatabaseAsync(
            new DatabaseBootstrapArgs("pg-failed-test") { SqlContent = "THIS IS NOT VALID SQL;" });
        Assert(!newDatabaseFailure.Status, "PostgreSQL failed-new-database test should report failure.");
        Assert(!await PostgreSqlDatabaseExistsAsync(connectionString, failedDatabaseName),
            "PostgreSQL should remove only the database created by the failed bootstrap call.");
    }
    finally
    {
        await DropPostgreSqlDatabaseAsync(connectionString, databaseName);
        await DropPostgreSqlDatabaseAsync(connectionString, failedDatabaseName);
    }
}

static async Task<bool> MariaDatabaseExistsAsync(string connectionString, string databaseName)
{
    var gateway = CreateGateway(connectionString, TargetDB.maria, "information_schema", "maria-exists");
    var count = await gateway.ScalarAsync<long>(
        "maria-exists",
        "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @database_name;",
        default,
        ("database_name", databaseName));
    return count == 1;
}

static async Task<bool> PostgreSqlDatabaseExistsAsync(string connectionString, string databaseName)
{
    var gateway = CreateGateway(connectionString, TargetDB.pgsql, "postgres", "pg-exists");
    var count = await gateway.ScalarAsync<long>(
        "pg-exists",
        "SELECT COUNT(*) FROM pg_database WHERE datname = @database_name;",
        default,
        ("database_name", databaseName));
    return count == 1;
}

static AdapterGateway CreateGateway(
    string connectionString,
    TargetDB target,
    string databaseName,
    string adapterKey)
{
    var gateway = new AdapterGateway(autoConfigure: false);
    gateway.Add(CreateAdapter(connectionString, target, databaseName, adapterKey));
    return gateway;
}

static AdapterConfig CreateAdapter(
    string connectionString,
    TargetDB target,
    string databaseName,
    string adapterKey)
{
    var info = AdapterGateway.SplitConnectionString(connectionString);
    info.Target = target;
    info.ConString = info.ConString.ReplaceValue(';', "database", databaseName)
        .ReplaceValue(';', "pooling", "false");
    return new AdapterConfig
    {
        AdapterKey = adapterKey,
        ConnectionString = info.ConString,
        ConnectionInfo = info,
        DBName = databaseName,
        DBType = target
    };
}

static async Task DropMariaDatabaseAsync(string connectionString, string databaseName)
{
    AssertSafeDatabaseName(databaseName);
    var gateway = CreateGateway(connectionString, TargetDB.maria, "information_schema", "maria-cleanup");
    await gateway.ExecAsync("maria-cleanup", $"DROP DATABASE IF EXISTS `{databaseName}`;");
}

static async Task DropPostgreSqlDatabaseAsync(string connectionString, string databaseName)
{
    AssertSafeDatabaseName(databaseName);
    var gateway = CreateGateway(connectionString, TargetDB.pgsql, "postgres", "pg-cleanup");
    await gateway.ExecAsync(
        "pg-cleanup",
        "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
        "WHERE datname = @database_name AND pid <> pg_backend_pid();",
        default,
        ("database_name", databaseName));
    await gateway.ExecAsync("pg-cleanup", $"DROP DATABASE IF EXISTS \"{databaseName}\";");
}

static string NewDatabaseName(string prefix) =>
    $"{prefix}_{Guid.NewGuid():N}";

static void AssertSafeDatabaseName(string databaseName)
{
    Assert(databaseName.Length <= 63 &&
           databaseName.All(character => char.IsAsciiLetterOrDigit(character) || character == '_') &&
           (databaseName.StartsWith("haley_bootstrap_maria_", StringComparison.Ordinal) ||
            databaseName.StartsWith("haley_bootstrap_pg_", StringComparison.Ordinal)),
        $"Refusing to remove unexpected database '{databaseName}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

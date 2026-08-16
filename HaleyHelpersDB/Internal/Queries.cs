using System;
using System.Globalization;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_MARIA {
        public const string ACQUIRE_BOOTSTRAP_LOCK = $@"SELECT GET_LOCK({LOCK_KEY}, {LOCK_TIMEOUT});";
        public const string RELEASE_BOOTSTRAP_LOCK = $@"SELECT RELEASE_LOCK({LOCK_KEY});";
        public const string DATABASE_EXISTS = $@"SELECT 1 FROM information_schema.schemata WHERE schema_name = {DATABASE_NAME} LIMIT 1;";
        public const string CREATE_DATABASE = "CREATE DATABASE IF NOT EXISTS {0} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        public const string DROP_DATABASE = "DROP DATABASE IF EXISTS {0};";

        public static string CreateDatabase(string quotedIdentifier) =>
            string.Format(CultureInfo.InvariantCulture, CREATE_DATABASE, quotedIdentifier);

        public static string DropDatabase(string quotedIdentifier) =>
            string.Format(CultureInfo.InvariantCulture, DROP_DATABASE, quotedIdentifier);
    }

    internal static class QRY_PGSQL {
        public const string ACQUIRE_BOOTSTRAP_LOCK = $@"SELECT pg_try_advisory_lock(hashtextextended({LOCK_KEY}, 0));";
        public const string RELEASE_BOOTSTRAP_LOCK = $@"SELECT pg_advisory_unlock(hashtextextended({LOCK_KEY}, 0));";
        public const string DATABASE_EXISTS = $@"SELECT 1 FROM pg_database WHERE datname = {DATABASE_NAME} LIMIT 1;";
        public const string CREATE_DATABASE = "CREATE DATABASE {0} TEMPLATE template0;";
        public const string DROP_DATABASE = "DROP DATABASE IF EXISTS {0};";

        public static string CreateDatabase(string quotedIdentifier) =>
            string.Format(CultureInfo.InvariantCulture, CREATE_DATABASE, quotedIdentifier);

        public static string DropDatabase(string quotedIdentifier) =>
            string.Format(CultureInfo.InvariantCulture, DROP_DATABASE, quotedIdentifier);
    }
}

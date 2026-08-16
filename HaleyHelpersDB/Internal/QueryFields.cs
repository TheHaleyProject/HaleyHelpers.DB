using System;
namespace Haley.Internal {
    internal static class QueryFields {
        public const string NAME = $@"@{nameof(NAME)}";
        public const string DATABASE_NAME = $@"@{nameof(DATABASE_NAME)}";
        public const string LOCK_KEY = $@"@{nameof(LOCK_KEY)}";
        public const string LOCK_TIMEOUT = $@"@{nameof(LOCK_TIMEOUT)}";
    }
}

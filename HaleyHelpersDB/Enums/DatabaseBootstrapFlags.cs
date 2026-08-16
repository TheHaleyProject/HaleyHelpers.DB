namespace Haley.Enums;

[Flags]
public enum DatabaseBootstrapFlags : long
{
    None = 0,
    CreateDatabaseIfMissing = 1L << 0,
    DropNewDatabaseOnFailure = 1L << 1
}

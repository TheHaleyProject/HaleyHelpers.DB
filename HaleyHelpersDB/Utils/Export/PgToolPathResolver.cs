using System.Runtime.InteropServices;

namespace Haley.Utils {
    using System.Runtime.InteropServices;

    public static class PgToolPathResolver {
        const string PG_DUMP = "pg_dump";
        const string PSQL = "psql";
        const string PG_FOLDER = "PostgreSQL";
        const string BIN_FOLDER = "bin";
        public static string ResolvePgDump(params int[] versions) => ResolvePgDump(null, versions);

        public static string ResolvePgDump(string[] drives, params int[] versions) {
            return ResolveTool(PG_DUMP, $@"{PG_DUMP}.exe", drives, versions);
        }

        public static string ResolvePsql(params int[] versions) => ResolvePsql(null, versions);

        public static string ResolvePsql(string[] drives, params int[] versions) {
            return ResolveTool(PSQL, $@"{PSQL}.exe", drives, versions);
        }

        private static string ResolveTool(
            string linuxToolName,
            string windowsToolName,
            string[]? drives,
            params int[] versions
        ) {
            if (versions == null || versions.Length == 0) versions = new int[] {21,20,19,18,17,16,15,14,13 };

            var orderedVersions = versions
                .Where(v => v > 0)
                .Distinct()
                .OrderByDescending(v => v)
                .ToArray();

            if (orderedVersions.Length == 0) {
                throw new ArgumentException("At least one valid PostgreSQL version must be provided.", nameof(versions));
            }

            IEnumerable<string> candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? BuildWindowsCandidates(windowsToolName, orderedVersions, drives)
                : BuildLinuxCandidates(linuxToolName, orderedVersions);

            foreach (var path in candidates) {
                if (File.Exists(path)) {
                    return path;
                }
            }

            //Fall back, just return the tool name and assume that the system will already have this path specified in the environment.
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? windowsToolName
                : linuxToolName;
        }

        private static IEnumerable<string> BuildWindowsCandidates(
            string exeName,
            int[] versions,
            string[]? drives
        ) {
            // Overload without drives:
            // Use current machine's Program Files location only.
            if (drives == null || drives.Length == 0) {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

                foreach (var version in versions) {
                    yield return Path.Combine(programFiles, PG_FOLDER, version.ToString(), BIN_FOLDER, exeName);
                }

                yield break;
            }

            // Overload with drives:
            // Build paths like C:\Program Files\PostgreSQL\17\bin\pg_dump.exe
            foreach (var drive in NormalizeDrives(drives)) {
                foreach (var version in versions) {
                    yield return $@"{drive}\Program Files\{PG_FOLDER}\{version}\{BIN_FOLDER}\{exeName}";
                }
            }
        }

        private static IEnumerable<string> BuildLinuxCandidates(string toolName, int[] versions) {
            foreach (var version in versions) {
                yield return $"/usr/pgsql-{version}/bin/{toolName}";
            }

            yield return $"/usr/bin/{toolName}";
        }

        private static IEnumerable<string> NormalizeDrives(IEnumerable<string> drives) {
            foreach (var raw in drives) {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var d = raw.Trim();

                d = d.TrimEnd('\\', '/');

                if (d.EndsWith(":")) {
                    d = d[..^1];
                }

                d = d.ToUpperInvariant();

                if (d.Length == 1 && char.IsLetter(d[0])) {
                    yield return $"{d}:";
                }
            }
        }
    }
}

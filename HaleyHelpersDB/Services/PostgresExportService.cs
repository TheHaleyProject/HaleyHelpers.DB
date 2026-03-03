using Haley.Enums;
using Haley.Models;
using Haley.Abstractions;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Haley.Utils;

namespace Haley.Services {
    public sealed class PostgresExportService {
        private readonly PgToolOptions _opt;

        public PostgresExportService(IOptions<PgToolOptions> options) {
            _opt = options.Value;
        }

        public async Task<ProcessRunResult> ExportAsync(PgExportRequest request, CancellationToken ct = default) {
            return request.Kind switch {
                PgExportKind.SchemaOnly
                or PgExportKind.DataOnly
                or PgExportKind.Full
                or PgExportKind.TableSchemaOnly
                or PgExportKind.TableDataOnly
                    => await RunPgDumpAsync(request, ct),

                PgExportKind.FunctionsOnly
                or PgExportKind.ProceduresOnly
                or PgExportKind.TypesOnly
                    => await RunPsqlAsync(request, ct),

                _ => throw new NotSupportedException($"Unsupported export kind: {request.Kind}")
            };
        }

        private async Task<ProcessRunResult> RunPgDumpAsync(PgExportRequest request, CancellationToken ct) {
            ValidateExecutablePath(_opt.PgDumpPath, nameof(_opt.PgDumpPath));

            string schema = request.Schema ?? _opt.DefaultSchema;

            var args = new List<string> {
            "--file", request.OutputFilePath,
            "--host", _opt.Host,
            "--port", _opt.Port.ToString(),
            "--username", _opt.Username,
            "--format", "p",
            "--encoding", request.Encoding ?? "UTF8"
        };

            if (request.NoOwner) args.Add("--no-owner");
            if (request.NoPrivileges) args.Add("--no-privileges");
            if (request.UseInserts) args.Add("--inserts");
            if (request.UseColumnInserts) args.Add("--column-inserts");

            switch (request.Kind) {
                case PgExportKind.SchemaOnly:
                args.Add("--schema-only");
                args.Add("--schema");
                args.Add(schema);
                break;

                case PgExportKind.DataOnly:
                args.Add("--data-only");
                args.Add("--schema");
                args.Add(schema);
                break;

                case PgExportKind.Full:
                args.Add("--schema");
                args.Add(schema);
                break;

                case PgExportKind.TableSchemaOnly:
                args.Add("--schema-only");
                AddTableArgs(args, schema, request.Tables);
                break;

                case PgExportKind.TableDataOnly:
                args.Add("--data-only");
                AddTableArgs(args, schema, request.Tables);
                break;

                default:
                throw new NotSupportedException($"Unsupported pg_dump export kind: {request.Kind}");
            }

            args.Add(_opt.Database); //Finally add the database name.

            //remember PgDump already writes to a file.. so, we need not do anything.
            return await RunProcessAsync(_opt.PgDumpPath, args, ct); //Regardless of whether we want Pgsql or pgdump, running the process remains the same.
        }

        private async Task<ProcessRunResult> RunPsqlAsync(PgExportRequest request, CancellationToken ct) {
            ValidateExecutablePath(_opt.PsqlPath, nameof(_opt.PsqlPath));

            string schema = request.Schema ?? _opt.DefaultSchema;

            string sql = request.Kind switch {
                PgExportKind.FunctionsOnly => GetFunctionsSql(schema),
                PgExportKind.ProceduresOnly => GetProceduresSql(schema),
                PgExportKind.TypesOnly => GetTypesSql(schema),
                _ => throw new NotSupportedException($"Unsupported psql export kind: {request.Kind}")
            };

            var args = new List<string> {
            "--host", _opt.Host,
            "--port", _opt.Port.ToString(),
            "--username", _opt.Username,
            "--dbname", _opt.Database,
            "-At",
            "-c", sql
        };

            //Psql never writes to a file.. So we receive the data and write it to a file.
            var result = await RunProcessAsync(_opt.PsqlPath, args, ct);

            if (result.Success) {
                await File.WriteAllTextAsync(request.OutputFilePath, result.StdOut, ct);
            }

            return result;
        }

        private async Task<ProcessRunResult> RunProcessAsync(string exePath, List<string> args, CancellationToken ct) {
            var psi = new ProcessStartInfo {
                FileName = exePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var arg in args) {
                psi.ArgumentList.Add(arg);
            }

            psi.Environment["PGPASSWORD"] = _opt.Password;

            using var proc = new Process {
                StartInfo = psi
            };

            if (!proc.Start()) {
                return new ProcessRunResult {
                    Success = false,
                    ExitCode = -1,
                    StdErr = $"Could not start process: {exePath}",
                    Executable = exePath,
                    Arguments = args
                };
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_opt.TimeoutSeconds));

            var stdOutTask = proc.StandardOutput.ReadToEndAsync();
            var stdErrTask = proc.StandardError.ReadToEndAsync();

            try {
                await proc.WaitForExitAsync(timeoutCts.Token); //Wait until the process is complete or the timeout happened and the token is cancelled.
            } catch (OperationCanceledException) {
                try {
                    if (!proc.HasExited) {
                        proc.Kill(entireProcessTree: true); //important otherwise we end up with abandone dprocesses.
                    }
                } catch {
                    // ignore kill failure
                }

                return new ProcessRunResult {
                    Success = false,
                    ExitCode = -2,
                    Executable = exePath,
                    Arguments = args,
                    StdOut = await stdOutTask.SafeReadAsync(),
                    StdErr = await stdErrTask.SafeReadAsync()
                };
            }

            return new ProcessRunResult {
                Success = proc.ExitCode == 0,
                ExitCode = proc.ExitCode,
                StdOut = await stdOutTask,
                StdErr = await stdErrTask,
                Executable = exePath,
                Arguments = args
            };
        }

        private static void AddTableArgs(List<string> args, string schema, List<string> tables) {
            if (tables == null || tables.Count == 0) {
                throw new InvalidOperationException("At least one table is required.");
            }

            foreach (var table in tables) {
                if (string.IsNullOrWhiteSpace(table)) continue;

                args.Add("--table");
                args.Add($"{schema}.{table}");
            }
        }

        private static void ValidateExecutablePath(string path, string fieldName) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new InvalidOperationException($"{fieldName} is not configured.");
            }
        }

        private static string GetFunctionsSql(string schema) => $@"
                SELECT pg_get_functiondef(p.oid)
                FROM pg_proc p
                JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname = '{EscapeSqlLiteral(schema)}'
                AND p.prokind = 'f'
                ORDER BY p.proname;";

        private static string GetProceduresSql(string schema) => $@"
                SELECT pg_get_functiondef(p.oid)
                FROM pg_proc p
                JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname = '{EscapeSqlLiteral(schema)}'
                AND p.prokind = 'p'
                ORDER BY p.proname;";

        private static string GetTypesSql(string schema) => $@"
                SELECT
                    'CREATE TYPE ' || quote_ident(n.nspname) || '.' || quote_ident(t.typname) ||
                    CASE
                        WHEN t.typtype = 'e' THEN ' AS ENUM (' ||
                            (
                                SELECT string_agg(quote_literal(e.enumlabel), ', ' ORDER BY e.enumsortorder)
                                FROM pg_enum e
                                WHERE e.enumtypid = t.oid
                            ) || ');'
                        ELSE ';'
                    END
                FROM pg_type t
                JOIN pg_namespace n ON n.oid = t.typnamespace
                WHERE n.nspname = '{EscapeSqlLiteral(schema)}'
                AND t.typtype IN ('e')
                ORDER BY t.typname;";

        private static string EscapeSqlLiteral(string value) {
            return value.Replace("'", "''");
        }
    }
}

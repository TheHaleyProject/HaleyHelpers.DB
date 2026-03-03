using Haley.Models;
using Haley.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static Haley.Utils.GeneralExtensions;

namespace Haley.Utils {

    public static class ExportExtensions {

        public static IServiceCollection AddPgExportService(this IServiceCollection services, IConfiguration configuration) {
            var opt = configuration.GetSection("PgTools").Get<PgToolOptions>() ?? new PgToolOptions();

            // resolve executable paths if missing
            if (string.IsNullOrWhiteSpace(opt.PgDumpPath)) opt.PgDumpPath = PgToolPathResolver.ResolvePgDump(new[] { "C", "D", "E" });

            if (string.IsNullOrWhiteSpace(opt.PsqlPath)) opt.PsqlPath = PgToolPathResolver.ResolvePsql(new[] { "C", "D", "E" });

            // fetch conn-string values if conn key is present
            PgToolOptions connValues = new();
            if (!string.IsNullOrWhiteSpace(opt.ConnectionKey)) {
                var rawConn = configuration.GetConnectionString(opt.ConnectionKey);

                if (!string.IsNullOrWhiteSpace(rawConn)) {
                    var conDic = rawConn.ToDictionarySplit(';');

                    if (conDic.TryGetValue("host",out var host) && host != null) connValues.Host = host.ToString() ?? "";
                    if (conDic.TryGetValue("port", out var port) && port != null && int.TryParse(port.ToString(), out var portVal)) connValues.Port = portVal;
                    if (conDic.TryGetValue("username",out var uname) && uname != null) connValues.Username = uname.ToString() ?? "";
                    if (conDic.TryGetValue("database",out var dbase) && dbase != null) connValues.Database = dbase.ToString() ?? "";
                    if (conDic.TryGetValue("password",out var pwd) && pwd != null) connValues.Password = pwd.ToString() ?? "";
                }
            }

            //// fetch adapter values if adapter key is present
            //PgToolOptions adapterValues = ResolveAdapter(opt.AdapterKey ?? "", agw);

            // merge field by field
            opt.Host = FirstNonEmpty(opt.Host, connValues.Host, "127.0.0.1");
            opt.Port = FirstPositive(opt.Port, connValues.Port, 5432);
            opt.Database = FirstNonEmpty(opt.Database, connValues.Database, "postgres");
            opt.Username = FirstNonEmpty(opt.Username, connValues.Username, "");
            opt.Password = FirstNonEmpty(opt.Password, connValues.Password, "");
            opt.DefaultSchema = FirstNonEmpty(opt.DefaultSchema, "public"); //Connvalues never populare schema

            Validate(opt);

            services.AddSingleton(Options.Create(opt));
            services.AddSingleton<PostgresExportService>();

            return services;
        }
        private static void Validate(PgToolOptions opt) {
            if (string.IsNullOrWhiteSpace(opt.PgDumpPath))
                throw new InvalidOperationException("Unable to resolve pg_dump path.");

            if (string.IsNullOrWhiteSpace(opt.PsqlPath))
                throw new InvalidOperationException("Unable to resolve psql path.");

            if (string.IsNullOrWhiteSpace(opt.Host))
                throw new InvalidOperationException("Unable to resolve PostgreSQL host.");

            if (opt.Port <= 0)
                throw new InvalidOperationException("Unable to resolve PostgreSQL port.");

            if (string.IsNullOrWhiteSpace(opt.Database))
                throw new InvalidOperationException("Unable to resolve PostgreSQL database.");

            if (string.IsNullOrWhiteSpace(opt.Username))
                throw new InvalidOperationException("Unable to resolve PostgreSQL username.");

            if (string.IsNullOrWhiteSpace(opt.Password))
                throw new InvalidOperationException("Unable to resolve PostgreSQL password.");
        }
    }
}

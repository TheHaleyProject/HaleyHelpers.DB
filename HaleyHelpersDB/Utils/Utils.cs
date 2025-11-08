using Haley.Abstractions;
using Haley.Internal;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Haley.Internal.QueryFields;

namespace Haley.Utils
{
    public static class CreationUtils
    {
        public static async Task<IFeedback> LoadSQL(this IAdapterGateway agw,LoadSqlArgs args) {
            var fb = new Feedback().SetSource("HALEY-DB-UTILS");
            try {
                if (args == null || string.IsNullOrWhiteSpace(args.Key)) throw new ArgumentNullException("Key cannot be empty.");
                //If the service or the db doesn't exist, we throw exception or else the system would assume that nothing is wrong. If they wish , they can still turn of the indexing.
                if (!agw.ContainsKey(args.Key)) throw new ArgumentException($@"Load SQL Failed.No adapter found for the given key {args.Key}");
                //Next step is to find out if the database exists or not? Should we even try to check if the database exists or directly run the sql script and create the database if it doesn't exists?
                args.DBName = agw[args.Key].Info?.DBName ?? args.FallBackDBName; //This is supposedly our db name.

                switch (args.TargetDB) {
                    case Enums.TargetDB.maria:
                    case Enums.TargetDB.mysql:
                    return await agw.LoadSQLMaria(args);
                    default:
                    throw new NotImplementedException($@"The target db type {args.TargetDB} is not implemented for loading the initial sql.");
                }
            } catch (Exception ex) {
                return fb.SetMessage(ex.Message).SetTrace(ex.StackTrace).SetCode((int)HttpStatusCode.InternalServerError);
            }
        }
        static async Task<IFeedback> LoadSQLMaria(this IAdapterGateway agw, LoadSqlArgs args) {
            var fb = new Feedback().SetSource("HALEY-DB-MARIA");
            if (!File.Exists(args.SQLPath)) throw new ArgumentException($@"SQL file is not found in {args.SQLPath}. Please check..");
            var exists = await agw.Scalar(new AdapterArgs(args.Key) { ExcludeDBInConString = true, Query = QRY_MARIA.SCHEMA_EXISTS }, (NAME, args.DBName));
            if (exists != null && exists.IsNumericType()) return fb.SetStatus(true).SetMessage($@"Database {args.DBName} already exists. No action taken.").SetCode((int)HttpStatusCode.Continue);
            
            //if the file exists, then run this file against the adapter gateway but ignore the db name.
            var content = File.ReadAllText(args.SQLPath);
            //We know that the file itself contains "dss_core" as the schema name. Replace that with new one.

            if (args.VariablesToReplace != null && args.VariablesToReplace.Count > 0) {
                foreach (var kvp in args.VariablesToReplace) {
                    if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
                    content = content.Replace(kvp.Key, kvp.Value);
                }
            }
            
            //?? Should we run everything in one go or run as separate statements???
            //if the input contains any delimiter or procedure, remove them.
            object queryContent = content;
            List<string> procedures = new();
            if (content.Contains("Delimiter", StringComparison.InvariantCultureIgnoreCase)) {
                //Step 1 : Remove delimiter lines
                content = Regex.Replace(content, @"DELIMITER\s+\S+", "", RegexOptions.IgnoreCase); //Remove the delimiter comments
                //Step 2 : Remove version-specific comments
                content = Regex.Replace(content, @"/\*!.*?\*/;", "", RegexOptions.Singleline);
                //Step 3 : Extract all Procedures
                string pattern = @"CREATE\s+PROCEDURE.*?END\s*//";
                var matches = Regex.Matches(content, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match match in matches) {
                    string proc = match.Value;
                    proc = proc.Replace("//", ";").Trim();
                    procedures.Add(proc);
                    content = content.Replace(match.Value, "");
                }
                // Step 4: Split remaining SQL by semicolon
                queryContent = Regex.Split(content, @";\s*(?=\n|$)", RegexOptions.Multiline);
                //queryContent = Regex.Split(content, @";\s*(?=\n|$)", RegexOptions.Multiline);
            }

            var handler = agw.GetTransactionHandler(args.Key);
            using (handler.Begin(true)) {
                await agw.NonQuery(new AdapterArgs(args.Key) { ExcludeDBInConString = true, Query = queryContent }.ForTransaction(handler));
                if (procedures.Count > 0) {
                    await agw.NonQuery(new AdapterArgs(args.Key) { ExcludeDBInConString = true, Query = procedures.ToArray() }.ForTransaction(handler));
                }
            }
            return fb.SetStatus(true).SetCode((int)HttpStatusCode.Created);
        }
    }
}

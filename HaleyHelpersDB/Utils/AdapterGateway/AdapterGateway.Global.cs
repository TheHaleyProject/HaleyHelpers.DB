using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;

namespace Haley.Utils {
    public partial class AdapterGateway {
        public static string ReplaceParameter(string connectionstring, string key, string value) {
            if (string.IsNullOrWhiteSpace(connectionstring) || string.IsNullOrWhiteSpace(value)) return connectionstring;
            //If user specifies a database, then remove the old value (if exists) and add the new database value
            string conStr = connectionstring;

            //REMOVE EXISTING
            if (conStr.Contains(key, StringComparison.OrdinalIgnoreCase)) {
                //remove that part.
                var allparts = conStr.Split(";");
                conStr = string.Join(";", allparts.Where(q => !q.Trim().StartsWith(key, StringComparison.OrdinalIgnoreCase)).ToArray());
            }

            //ADD NEW VALUE. ? Where is the equal to sign?
            conStr += $@";{key}{value}";
            return conStr;
        }

        public static string ParseConnectionString(string connectionstring,string field_name) {
            if (string.IsNullOrWhiteSpace(connectionstring) || string.IsNullOrWhiteSpace(field_name)) return string.Empty;
            //If user specifies a database, then remove the old value (if exists) and add the new database value
            string conStr = connectionstring;
            if (conStr.Contains(field_name, StringComparison.OrdinalIgnoreCase)) {
                //remove that part.
                var allparts = conStr.Split(";");
                var kvp=  allparts.FirstOrDefault(q => q.Trim().StartsWith(field_name, StringComparison.OrdinalIgnoreCase));
                if (kvp != null) {
                   return kvp.Split("=")[1];
                }
            }
            return string.Empty;
        }

        private static (string cstr, TargetDB dbtype) SplitConnectionString(string connectionString) {
            string conStr = connectionString;
            TargetDB targetType = TargetDB.unknown;
            //Fetch and remove the dbtype.
            if (conStr.Contains(DBTYPE_KEY, StringComparison.OrdinalIgnoreCase)) {
                //remove that part.
                var allparts = conStr.Split(";");

                switch (Convert.ToString(allparts.FirstOrDefault(q => q.Trim().StartsWith(DBTYPE_KEY, StringComparison.OrdinalIgnoreCase))?.Replace(DBTYPE_KEY, "",StringComparison.OrdinalIgnoreCase))) {
                    case "maria":
                    targetType = TargetDB.maria;
                    break;

                    case "mssql":
                    targetType = TargetDB.mssql;
                    break;

                    case "pgsql":
                    targetType = TargetDB.pgsql;
                    break;

                    case "mysql":
                    default:
                    targetType = TargetDB.mysql;
                    break;
                }
                conStr = string.Join(";", allparts.Where(q => !q.Trim().StartsWith(DBTYPE_KEY,StringComparison.OrdinalIgnoreCase)).ToArray()); //Without the dbtype.
            }
            return (conStr, targetType);
        }
    }
}
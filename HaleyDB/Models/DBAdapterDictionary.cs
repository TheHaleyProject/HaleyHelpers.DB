using Haley.Enums;
using Haley.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Models {

    public class DBAdapterDictionary : ConcurrentDictionary<string, DBAdapter> {
        private IConfigurationRoot _cfgRoot;
        private const string DBTYPE_KEY = "dbtype=";

        public DBAdapterDictionary() {
        }

        #region Global Methods

        public static IConfigurationRoot GenerateConfigurationRoot(string[] jsonPaths, string basePath = null) {
            var builder = new ConfigurationBuilder();
            if (basePath == null) basePath = AssemblyUtils.GetBaseDirectory(); ; //Hopefully both interface DLL and the main app dll are in same directory where the json files are present.
            builder.SetBasePath(basePath); // let us load the file from a specific directory

            foreach (var path in jsonPaths) {
                if (path == null) continue;
                string finalFilePath = path.Trim();
                if (!finalFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && finalFilePath != null) {
                    finalFilePath += ".json";
                }

                //Assume it is an absolute path
                if (!File.Exists(finalFilePath)) {
                    //We assumed it was an abolute path and it doesn't exists.. What if it is a relative path?
                    //Combine with base path and check if it exists.
                    if (!File.Exists(Path.Combine(basePath, finalFilePath!))) continue;
                }
                //If we reach here then the file is present, regardless of whether it is absolute or relative.

                builder.AddJsonFile(finalFilePath);
            }
            return builder.Build();
        }

        private static (TargetDB dbtype, string cstr) SplitConnectionString(string connectionString) {
            string conStr = connectionString;
            TargetDB targetType = TargetDB.unknown;
            //Fetch and remove the dbtype.
            if (conStr.Contains(DBTYPE_KEY, StringComparison.OrdinalIgnoreCase)) {
                //remove that part.
                var allparts = conStr.Split(";");

                switch (Convert.ToString(allparts.FirstOrDefault(q => q.StartsWith(DBTYPE_KEY))?.Replace(DBTYPE_KEY, ""))) {
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
                conStr = string.Join(";", allparts.Where(q => !q.StartsWith(DBTYPE_KEY)).ToArray()); //Without the dbtype.
            }
            return (targetType, conStr);
        }

        public static (TargetDB dbtype, string cstr) SanitizeConnectionString(string connectionstring, string dbname = null) {
            string conStr = connectionstring;
            var tuple1 = SplitConnectionString(conStr); // to remove the dbtype key.
            conStr = tuple1.cstr;

            //If user specifies a database, then remove the old value (if exists) and add the new database value
            if (dbname != null && conStr != null) {
                //If it already contains the databasevalue, delete it first.
                if (conStr.Contains("database=", StringComparison.OrdinalIgnoreCase)) {
                    //remove that part.
                    var allparts = conStr.Split(";");
                    conStr = string.Join(";", allparts.Where(q => !q.StartsWith("database=")).ToArray());
                }
                conStr += $@";database={dbname}";
            }
            return (tuple1.dbtype, conStr);
        }

        public static (TargetDB dbtype, string cstr) ParseConnectionString(IConfigurationRoot cfgRoot, string json_key, string dbname = null) {
            if (cfgRoot == null) return (TargetDB.unknown, null);
            if (json_key == null) throw new ArgumentException("Json Key cannot be empty when parsing from Configuration Root");
            string conStr = cfgRoot.GetConnectionString(json_key); //From the configuration root.
            if (conStr == null) throw new ArgumentException($@"Unable to find any connection string in the Configuration root Json with key {json_key}");

            var tuple1 = SanitizeConnectionString(conStr, dbname);
            return tuple1;
        }

        #endregion Global Methods

        #region Add or Generate Connections

        public DBAdapterDictionary Add(Enum key, string connectionStr, ILogger logger = null) {
            return Add(key, connectionStr, out _, logger);
        }

        public DBAdapterDictionary Add(string key, string connectionStr, ILogger logger = null) {
            return Add(key, connectionStr, out _, logger);
        }

        public DBAdapterDictionary Add(Enum key, string connectionStr, out DBAdapter adapter, ILogger logger = null) {
            if (key == null) throw new ArgumentNullException("key");
            return Add(key.GetKey(), connectionStr, out adapter, logger);
        }

        public DBAdapterDictionary Add(string key, string connectionStr, out DBAdapter adapter, ILogger logger = null) {
            var tuple1 = SanitizeConnectionString(connectionStr);
            return Add(key, tuple1.cstr, tuple1.dbtype, null, out adapter, logger);
        }

        public DBAdapterDictionary Generate(Enum key, string json_key, string dbname = null, ILogger logger = null) {
            return Generate(key, json_key, out _, dbname, logger);
        }

        public DBAdapterDictionary Generate(string key, string json_key, string dbname = null, ILogger logger = null) {
            return Generate(key, json_key, out _, dbname, logger);
        }

        public DBAdapterDictionary Generate(Enum key, string json_key, out DBAdapter adapter, string dbname = null, ILogger logger = null) {
            if (key == null) throw new ArgumentNullException("key");
            return Generate(key.GetKey(), json_key, out adapter, dbname, logger);
        }

        public DBAdapterDictionary Generate(string key, string json_key, out DBAdapter adapter, string dbname = null, ILogger logger = null) {
            if (GetConfigurationRoot() == null) throw new ArgumentException($@"Internal Config root is empty. Hint: Look at {nameof(SetConfigurationRoot)}");
            var tuple1 = ParseConnectionString(GetConfigurationRoot(), json_key, dbname);
            return Add(key, tuple1.cstr, tuple1.dbtype, json_key, out adapter, logger);
        }

        #endregion Add or Generate Connections

        private DBAdapterDictionary Add(string key, string connectionStr, TargetDB dbtype, string json_key, out DBAdapter adapter, ILogger logger = null) {
            if (key == null) throw new ArgumentNullException("key");
           
            // In case dbtype is unknown then it should not register as we don't know which database handler to use.
            if (dbtype == TargetDB.unknown) {
                throw new ArgumentException("Missing: Value for DBTYPE which is needed to decide the type of database to connect to.");
            }
            adapter = new DBAdapter(connectionStr, json_key, dbtype);

            if (ContainsKey(key)) {
                //remove the adapter
                if (!TryRemove(key, out _)) {
                    throw new ArgumentException($@"Key {key} already exists and unable to replace it as well.");
                }; //remove the item.
            }

            if (TryAdd(key, adapter)) {
                return this; //Trying to add the key and adapter here
            }
            throw new ArgumentException("Unable to add DBAdapter to dictionary.");
        }

        #region Configuration Root Management

        public IConfigurationRoot GetConfigurationRoot() {
            return _cfgRoot;
        }

        public DBAdapterDictionary SetConfigurationRoot(string[] jsonPaths, string basePath = null) {
            return SetConfigurationRoot(GenerateConfigurationRoot(jsonPaths, basePath));
        }

        public DBAdapterDictionary SetConfigurationRoot(IConfigurationRoot cfgRoot) {
            if (cfgRoot == null) throw new ArgumentNullException(nameof(cfgRoot));
            _cfgRoot = cfgRoot;
            return this;
        }

        #endregion Configuration Root Management

        #region Connection Utils Management

        public DBAdapterDictionary UpdateAdapter() {
            return UpdateAdapter(_cfgRoot);
        }

        public DBAdapterDictionary UpdateAdapter(IConfigurationRoot configRoot) {
            if (configRoot == null) throw new ArgumentNullException(nameof(configRoot));
            foreach (var kvp in this) {
                try {
                    var info = kvp.Value.GetInfo();
                    var con_tuple = ParseConnectionString(configRoot, info.key, info.dbName);
                    kvp.Value.UpdateConnectionString(con_tuple.cstr, con_tuple.dbtype);
                } catch (Exception) {
                    //Don't throw exception while updating adapter.
                    continue;
                }
            }
            return this;
        }

        #endregion Connection Utils Management
    }
}
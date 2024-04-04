using Haley.Abstractions;
using Haley.Enums;
using Haley.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlX.XDevAPI.Common;
using Org.BouncyCastle.Asn1.Esf;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Net.Http;
using System.Security.Cryptography;

namespace Haley.Models {

    public delegate void DictionaryUpdatedEvent();

    public class DBAService : ConcurrentDictionary<string, DBAdapter>, IDBService {
        public static DBAService Instance => GetInstance();
        static DBAService _instance;
        static DBAService GetInstance() {
            if (_instance == null) { _instance = new DBAService(); }
            return _instance;
        }

        const string DBA_ENTRIES = "DbaEntries";
        const string DBNAME_KEY = "database=";
        const string DBTYPE_KEY = "dbtype=";
        const string SEARCHPATH_KEY = "searchpath=";

        IConfigurationRoot _cfgRoot;
        IDBServiceUtil _util;

        ConcurrentDictionary<string, (string cstr, TargetDB dbtype)> connectionstrings = new ConcurrentDictionary<string, (string cstr, TargetDB dbtype)>();
        public DBAService() {
        }

        public event DictionaryUpdatedEvent Updated;
        #region Global Methods

        public static IConfigurationRoot GenerateConfigurationRoot(string[] jsonPaths = null, string basePath = null) {
            var builder = new ConfigurationBuilder();
            var jsonlist = jsonPaths?.ToList() ?? new List<string>();
            if (basePath == null) basePath = AssemblyUtils.GetBaseDirectory(); ; //Hopefully both interface DLL and the main app dll are in same directory where the json files are present.
            builder.SetBasePath(basePath); // let us load the file from a specific directory

            if (jsonlist == null || jsonlist.Count < 1) {
                jsonlist = new List<string>() {"appsettings", "connections" }; //add these two default jsons.
            }

            if (!jsonlist.Contains("appsettings")) jsonlist.Add("appsettings");
            if (!jsonlist.Contains("connections")) jsonlist.Add("connections");

            foreach (var path in jsonlist) {
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

            //ADD NEW VALUE.
            conStr += $@";{key}{value}";
            return conStr;
        }

        public static string GetDBName(string connectionstring) {
            if (string.IsNullOrWhiteSpace(connectionstring)) return string.Empty;
            //If user specifies a database, then remove the old value (if exists) and add the new database value
            string conStr = connectionstring;
            if (conStr.Contains(DBNAME_KEY, StringComparison.OrdinalIgnoreCase)) {
                //remove that part.
                var allparts = conStr.Split(";");
                var kvp=  allparts.FirstOrDefault(q => q.Trim().StartsWith(DBNAME_KEY,StringComparison.OrdinalIgnoreCase));
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


        #endregion Global Methods

        #region Add or Generate Connections

        public DBAService Configure() {
            return Configure(false);
        }

        public bool IsShaValid(string adapterKey,string sha) {
            if (string.IsNullOrWhiteSpace(adapterKey) || string.IsNullOrWhiteSpace(sha) || !this.ContainsKey(adapterKey)) return false;
            return this[adapterKey].Entry.Sha == sha;
        }

        DBAService Configure(bool updateOnly = false) {
            ParseConnectionStrings(updateOnly); //Load all latest connection string information into memory.
            if (connectionstrings == null) throw new ArgumentNullException(nameof(connectionstrings));
            //Supposed to read the json files and then generate all the adapters.
            try {
                var root = GetConfigurationRoot(updateOnly);
                var entries = root.GetSection(DBA_ENTRIES)?.Get<DbaEntry[]>(); //Fetch all entry information.
                if (entries == null) return this;
                foreach (var entry in entries) {

                    if (string.IsNullOrWhiteSpace(entry.AdapterKey) || string.IsNullOrWhiteSpace(entry.ConnectionKey)) continue;
                    //based upon the connection string key in the entry, fetch the corresponding Connection string and it's dbtype from the already parsed connection strings.
                    if (connectionstrings.TryGetValue(entry.ConnectionKey, out var connectionData)) {
                        entry.DBType = connectionData.dbtype;
                        //If user didn't specify dbname , then take it from the connectionstring itself.
                        var constr = connectionData.cstr;
                        if (!string.IsNullOrWhiteSpace(entry.DBName)) {
                            //replace this name in the connection string.
                            constr = ReplaceParameter(constr, DBNAME_KEY, entry.DBName);
                        } else {
                            //Let us try to fetch the dbname from the input.
                            entry.DBName = GetDBName(constr);
                        }
                        entry.ConnectionString = constr;

                        //For postgres, add schema as well
                        if (entry.DBType == TargetDB.pgsql && !string.IsNullOrWhiteSpace(entry.SchemaName)) {
                            entry.ConnectionString = ReplaceParameter(entry.ConnectionString, SEARCHPATH_KEY, entry.SchemaName);
                        }

                        // In case dbtype is unknown then it should not register as we don't know which database handler to use.
                        if (entry.DBType == TargetDB.unknown) {
                            throw new ArgumentException($@"Missing: Value for DBTYPE which is needed to decide the type of database to connect to. Entry {entry.ConnectionKey} - {entry.AdapterKey}");
                        }

                        if (this.ContainsKey(entry.AdapterKey) && updateOnly) {
                            //Now this will be an update.
                            this[entry.AdapterKey].UpdateDBEntry(entry);
                        } else {
                            Add(entry);
                        }
                    }
                }
            } catch (Exception) {
                throw;
            }
            return this;
        }

        void ParseConnectionStrings(bool reload = false) {
            var root = GetConfigurationRoot(reload);
            var allconnection = root.GetSection("ConnectionStrings");
            connectionstrings = new ConcurrentDictionary<string, (string cstr, TargetDB dbtype)>(); //reset.
            foreach (var item in allconnection.GetChildren()) {
                connectionstrings.TryAdd(item.Key, SplitConnectionString(item.Value));
            }
        }
        #endregion Add or Generate Connections

        DBAService Add(DbaEntry entry) {
            var adapter = new DBAdapter(entry);

            if (ContainsKey(entry.AdapterKey)) {
                //remove the adapter
                if (!TryRemove(entry.AdapterKey, out _)) {
                    throw new ArgumentException($@"Key {entry.AdapterKey} already exists and unable to replace it as well.");
                }; //remove the item.
            }

            if (TryAdd(entry.AdapterKey, adapter)) {
                return this; //Trying to add the key and adapter here
            }
            throw new ArgumentException("Unable to add DBAdapter to dictionary.");
        }

        #region Configuration Root Management

        public IConfigurationRoot GetConfigurationRoot(bool reload = false) {
            if (_cfgRoot == null) {
                //Set default configuration root.
                SetConfigurationRoot(null, null);
            } else {
                if (reload) _cfgRoot.Reload();
            }
            return _cfgRoot;
        }

        public DBAService SetConfigurationRoot(string[] jsonPaths, string basePath = null) {
            SetConfigurationRoot(GenerateConfigurationRoot(jsonPaths, basePath));
            return this;
        }

        public DBAService SetConfigurationRoot(IConfigurationRoot cfgRoot) {
            if (cfgRoot == null) throw new ArgumentNullException(nameof(cfgRoot));
            _cfgRoot = cfgRoot;
            return this;
        }

        #endregion Configuration Root Management

        #region Connection Utils Management

        public DBAService UpdateAdapter() {
            Configure(true);
            Updated?.Invoke();
            return this;
        }
        #endregion Connection Utils Management

        #region Execution
        public void SetServiceUtil(IDBServiceUtil util) {
            _util = util;
        }

        public async Task<object> Read(string dba_key, ILogger logger, string query,  params (string key, object value)[] parameters) {
            return await ExecuteInternal(true, dba_key, logger, query, ResultFilter.None, parameters);
        }

        public async Task<object> NonQuery(string dba_key, ILogger logger, string query, params (string key, object value)[] parameters) {
            return await ExecuteInternal(false, dba_key, logger, query, ResultFilter.None, parameters);
        }

        public async Task<object> Read(string dba_key, ILogger logger, string query, ResultFilter filter , params (string key, object value)[] parameters) {
            return await ExecuteInternal(true, dba_key, logger, query, filter, parameters);
        }

        public async Task<object> NonQuery(string dba_key, ILogger logger, string query, ResultFilter filter , params (string key, object value)[] parameters) {
            return await ExecuteInternal(false, dba_key, logger, query, filter, parameters);
        }

        public async Task<object> GetFirst(object input, ResultFilter filter = ResultFilter.None) {
            if (_util != null) return await _util.GetFirst(input, filter); //we know that it is not a dictionary
            return input;
        }

        async Task<object> ExecuteInternal(bool isread, string dba_key, ILogger logger, string query, ResultFilter filter, params (string key, object value)[] parameters) {
            if (string.IsNullOrWhiteSpace(dba_key)) throw new ArgumentException("dba_key cannot be empty");
            if (!ContainsKey(dba_key)) throw new ArgumentNullException($@"{dba_key} is not found in the dictionary");
            try {
                object result = null;
                switch (isread) {
                    case true:
                    result= (await this[dba_key]?.ExecuteReader(query, null, parameters))?.Select(true)?.Convert()?.ToList();
                    break;
                    case false:
                    default:
                    result = await this[dba_key]?.ExecuteNonQuery(query, null, parameters);
                    break;
                }
                return await GetFirst(result,filter);
            } catch (Exception ex) {
                logger.LogError(ex.StackTrace);
                return await GetFirst(new DBAError(ex.Message));
            }
        }
        #endregion
    }
}
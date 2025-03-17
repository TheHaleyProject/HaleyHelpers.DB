using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Utils {

    public delegate void DictionaryUpdatedEvent();

    //DB ADAPTER SERVICE
    public class DBService : ConcurrentDictionary<string, IDBAdapter>, IDBService {

        //private static DBService _instance;
        //public static DBService Instance {
        //    get {
        //        if (_instance == null) { _instance = new DBService(); }
        //        return _instance;
        //    }
        //    set { if (_instance == null) _instance = value; }
        //}

        //public IDBService SetAsInstance() {
        //    Instance = this; 
        //    return this;
        //}

        const string DBA_ENTRIES = "DbaEntries";
        const string DBNAME_KEY = "database=";
        const string DBTYPE_KEY = "dbtype=";
        const string SEARCHPATH_KEY = "searchpath=";

        IConfigurationRoot _cfgRoot;
        IDBServiceUtil _util;

        ConcurrentDictionary<string, (string cstr, TargetDB dbtype)> connectionstrings = new ConcurrentDictionary<string, (string cstr, TargetDB dbtype)>();
        public DBService(bool autoConfigure = true) {
            //Id = Guid.NewGuid();
            if (autoConfigure) Configure();
        }

        public Guid Id { get; } = Guid.NewGuid();

        public bool ThrowCRUDExceptions { get; set; }

        public event DictionaryUpdatedEvent Updated;
        #region Global Methods

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


        #endregion Global Methods

        #region Add or Generate Connections

        public IDBService Configure() {
            return Configure(false);
        }

        public bool IsShaValid(string adapterKey,string sha) {
            if (string.IsNullOrWhiteSpace(adapterKey) || string.IsNullOrWhiteSpace(sha) || !this.ContainsKey(adapterKey)) return false;
            return this[adapterKey].Info.Sha == sha;
        }

        public string GetSchemaName(string adapterKey) {
            if (!this.ContainsKey(adapterKey)) return null;
            return this[adapterKey].Info.SchemaName;
        }

        IDBService Configure(bool reload) {
            ParseConnectionStrings(reload); //Load all latest connection string information into memory.
            if (connectionstrings == null) throw new ArgumentNullException(nameof(connectionstrings));
            //Supposed to read the json files and then generate all the adapters.
            try {
                var root = GetConfigurationRoot(reload);
                var entries = root.GetSection(DBA_ENTRIES)?.Get<DBAdapterInfo[]>(); //Fetch all entry information.
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
                            entry.DBName = ParseConnectionString(constr,DBNAME_KEY);
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

                        if (this.ContainsKey(entry.AdapterKey) && reload) {
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

        public IDBService Add(IDBAdapterInfo entry, bool replace = true) {
            var adapter = new DBAdapter(entry);

            if (!replace && ContainsKey(entry.AdapterKey)) return this;

            if (replace && ContainsKey(entry.AdapterKey)) {
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

        public IConfigurationRoot GetConfigurationRoot(bool reload = false, bool force_reload = false) {
            if (_cfgRoot == null || force_reload) {
                //Set default configuration root.
                SetConfigurationRoot(null, null);
            } else {
                if (reload) _cfgRoot.Reload();
            }
            return _cfgRoot;
        }

        public IDBService SetConfigurationRoot(string[] jsonPaths, string basePath = null) {
            SetConfigurationRoot(ResourceUtils.GenerateConfigurationRoot(jsonPaths, basePath));
            return this;
        }

        public IDBService SetConfigurationRoot(IConfigurationRoot cfgRoot) {
            if (cfgRoot == null) throw new ArgumentNullException(nameof(cfgRoot));
            _cfgRoot = cfgRoot;
            return this;
        }

        #endregion Configuration Root Management

        #region Connection Utils Management

        public IDBService UpdateAdapter() {
            Configure(true);
            Updated?.Invoke();
            return this;
        }
        #endregion Connection Utils Management

        #region Execution

        protected virtual IDBService GetDBService() { return this; }
        public ITransactionHandler GetTransactionHandler(string adapterKey) {
            return new TransactionHandler(GetAdapterInfo(adapterKey)) {_dbs = GetDBService() }; 
        }
        protected IDBAdapterInfo GetAdapterInfo(string adapterKey) {
            if (string.IsNullOrWhiteSpace(adapterKey) || !ContainsKey(adapterKey)) throw new ArgumentNullException($@"Adapter key not registered {adapterKey}");
           return this[adapterKey].Info.Clone() as IDBAdapterInfo; //All connection strings properly parsed.
        }
        public void SetServiceUtil(IDBServiceUtil util) {
            _util = util;
        }

        public Task<object> Read(IParameterBase input, string query, params (string key, object value)[] parameters) {
            return Read(input.Convert(query), parameters);
        }

        public Task<object> Scalar(IParameterBase input, string query, params (string key, object value)[] parameters) {
            return Scalar(input.Convert(query), parameters);
        }

        public Task<object> NonQuery(IParameterBase input, string query, params (string key, object value)[] parameters) {
            return NonQuery(input.Convert(query), parameters);
        }

        public async Task<object> Read(IAdapterParameter input,  params (string key, object value)[] parameters) {
            if (input is AdapterParameter inputEx) inputEx.ReturnsResult = true;
            return await ExecuteInternal(input, parameters);
        }

        public async Task<object> Scalar(IAdapterParameter input, params (string key, object value)[] parameters) {
            if (input is AdapterParameter inputEx) {
                inputEx.ReturnsResult = true;
                inputEx.IsScalar = true;
            }
            return await ExecuteInternal(input, parameters);
        }

        public async Task<object> NonQuery(IAdapterParameter input, params (string key, object value)[] parameters) {
            if (input is AdapterParameter inputEx) inputEx.ReturnsResult = false;
            return await ExecuteInternal(input, parameters);
        }

        public async Task<object> GetFirst(object input, ResultFilter filter = ResultFilter.None) {
            //Now, apply internal methods to get the result
            input = input.ApplyFilter(filter);
            if (_util != null) return _util.Convert(input); //we know that it is not a dictionary
            return input;
        }

        //(string key, object value, ParameterDirection direction)[] ParamAdapter(params (string key, object value)[] parameters) {
        //    List<(string key, object value, ParameterDirection direction)> result = new List<(string key, object value, ParameterDirection direction)>();
        //    foreach (var item in parameters) {
        //        result.Add((item.key, item.value, ParameterDirection.Input));
        //    }
        //    return result.ToArray();
        //}

        IDBAdapter GetAdapter(IAdapterParameter input) {
            if (input is AdapterParameter adp && adp.Adapter != null) return adp.Adapter;
            if (string.IsNullOrWhiteSpace(input.Key)) throw new ArgumentException("Adapter key cannot be empty");
            if (!ContainsKey(input.Key)) throw new ArgumentNullException($@"DBAKey missing: {input.Key} is not found in the dictionary");
            return this[input.Key];
        }

        async Task<object> ExecuteInternal(IAdapterParameter input, params (string key, object value)[] parameters) {
            try {
                object result = null;
                if (input is AdapterParameter inputEx && inputEx.ReturnsResult) {
                    if (inputEx.IsScalar) {
                        result = (await GetAdapter(input).Scalar(input, parameters));
                    } else {
                        result = ((DataSet)await GetAdapter(input).Read(input, parameters))?.Select(true)?.Convert()?.ToList();
                    }
                } else {
                    result = await GetAdapter(input).NonQuery(input, parameters);
                }
                return await GetFirst(result,input.Filter);
            } catch (Exception ex) {
                input.Logger?.LogError($@"Error for: {input.Query}");
                input.Logger?.LogError(ex.Message);
                input.Logger?.LogError(ex.StackTrace);
                if (ThrowCRUDExceptions) throw ex;
                return await GetFirst(new FeedbackError(ex.Message));
            }
        }
        #endregion
    }
}
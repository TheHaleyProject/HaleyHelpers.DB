using Haley.Abstractions;
using Haley.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Utils {
    public class DBMService : ConcurrentDictionary<string, IDBModule>, IDBModuleService {

        private IDBService _dbService;
        public IDBService DBService {
            get {
                if (_dbService == null) _dbService = DBAService.Instance; 
                return _dbService; 
            }
        }

        //Try to get the dependency injection resolver to build the DBModule? Will it be an overkill??
        //How do we pass the DB Service to the modules? Should we delegate this task to the Module? or is it fine to pass the service from here itself?
        public bool TryRegisterModule<M>(Enum key, M module) where M : IDBModule {
            return TryRegisterModule(key.GetKey(),module);
        }

        public bool TryRegisterModule<M>(string key, M module) where M : IDBModule {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (this.ContainsKey(key)) throw new InvalidDataException($@"Key {key} already exists.");
            if (module is DefaultModule)
            return this.TryAdd(key, module);
        }

        public DBMService(IDBService dbs) {
            _dbService = dbs;
        }
    }
}

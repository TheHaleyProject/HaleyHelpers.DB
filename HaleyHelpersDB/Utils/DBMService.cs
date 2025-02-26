using Haley.Abstractions;
using Haley.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        public bool TryRegisterModule<M>(ModuleInfo<M> info) where M : IDBModule {
            if (info == null) throw new ArgumentNullException(nameof(ModuleInfo<M>));
            if (string.IsNullOrWhiteSpace(info.Key)) throw new ArgumentNullException(nameof(info.Key));
            if (this.ContainsKey(info.Key)) throw new InvalidDataException($@"Key {info.Key} already exists.");
            if (info.Module is DefaultModule defMdl) {
                if (info.Seed == null) info.Seed = new Dictionary<string, object>();
                if (!info.Seed.ContainsKey("dbs")) info.Seed.TryAdd("dbs", _dbService); //Add dbservice
                if (info.Seed["dbs"] == null || !info.Seed["dbs"].GetType().IsAssignableFrom(typeof(IDBService))) {
                    info.Seed["dbs"] = _dbService;
                }
                defMdl.SetSeed(info.Seed); //Set the seed only via this service.
                defMdl.Initialize(); //Default module initialization
            }
            //todo: think of better ways to handle this registration.
            return this.TryAdd(info.Key,info.Module);
        }

        public IDBModule this[Enum key] {
            get {
                if (!this.ContainsKey(key.GetKey())) throw new KeyNotFoundException();
                return this[key.GetKey()];
            }
            set {
                if (!this.ContainsKey(key.GetKey())) throw new KeyNotFoundException();
                this[key.GetKey()] = value;
            }
        }

        public bool ContainsKey(Enum key) {
            return this.ContainsKey(key.GetKey());
        }

        public DBMService(IDBService dbs) {
            _dbService = dbs;
        }
    }
}

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
    public class DBServiceEx : DBService, IDBServiceEx {
        public static new DBServiceEx Instance => GetInstance();
        static DBServiceEx _instance = new DBServiceEx();
        static DBServiceEx GetInstance() {
            if (_instance == null) { _instance = new DBServiceEx(); }
            return _instance;
        }

        ConcurrentDictionary<Type, IDBModule> _dic = new ConcurrentDictionary<Type, IDBModule>();

        //Try to get the dependency injection resolver to build the DBModule? Will it be an overkill??
        //How do we pass the DB Service to the modules? Should we delegate this task to the Module? or is it fine to pass the service from here itself?
        public async Task<bool> TryRegisterModule<M, P>(M module, Dictionary<string, object> seed)
            where P : IModuleParameter
            where M : IDBModule<P> //Register a module
        {
            var argT = typeof(P);
            if (_dic.ContainsKey(argT)) return false;
            if (seed == null) seed = new Dictionary<string, object>();
            if (!seed.ContainsKey("dbs") || seed["dbs"].GetType().IsAssignableFrom(typeof(IDBService))) {
                seed.TryAdd("dbs", this);
            }

            if (module is DefaultDBM defDBM) {
                defDBM.SetSeed(seed); //Set the seed only via this service.
                await defDBM.Initialize(); //Default module initialization
            }
            return _dic.TryAdd(argT, module);
            //todo: think of better ways to handle this registration.
        }

        public Task<object> Execute<P>(P arg) where P : IModuleParameter {
            var argT = typeof(P);
            if (!_dic.ContainsKey(argT)) throw new KeyNotFoundException($@"{argT}");
            return _dic[argT].Execute(arg);
        }

        public DBServiceEx() {
        }
    }
}

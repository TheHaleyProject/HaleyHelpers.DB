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
        public async Task<(bool status, string msg)> TryRegisterModule<M>(M module, Dictionary<string, object> seed)
            where M : IDBModule //Register a module
        {
            try {
                //First try to see if the Module has a generic parameter, if yes, then focus on getting it else check if the user has defined any parameter type directly.
                var dbInterface = typeof(M).GetInterfaces()?.FirstOrDefault(p =>
                    p.IsGenericType &&
                    p.Name == $@"{nameof(IDBModule)}`1");

                if (dbInterface == null) return (false, $@"The module should implement the generic interface{nameof(IDBModule)}<> ");
                Type mpT = dbInterface.GetGenericArguments().FirstOrDefault() ?? module.ParameterType;
                if (mpT == null) return (false, $@"The type argument of {nameof(IDBModule)} should implement {nameof(IModuleParameter)}");
                var dbcT = mpT.GetInterfaces()?.FirstOrDefault(p => p.IsGenericType && p.Name == $@"{nameof(IModuleParameter)}`1");
                if (dbcT == null) return (false, $@"The type argument of {nameof(IDBModule)} should implement {nameof(IModuleParameter)} "); ; //Even after above step if we dont' get the parameter type, don't register it.
                if (_dic.ContainsKey(mpT)) return (false, $@"{mpT} is already registered.");
                if (seed == null) seed = new Dictionary<string, object>();
                if (!seed.ContainsKey("dbs") || seed["dbs"].GetType().IsAssignableFrom(typeof(IDBService))) {
                    seed.TryAdd("dbs", this);
                }

                if (module is DefaultDBM defDBM) {
                    defDBM.SetSeed(seed); //Set the seed only via this service.
                    await defDBM.Initialize(); //Default module initialization
                }
                var status = _dic.TryAdd(mpT, module);
                return (status, status ? "Success" : "Failed to register the module");
                //todo: think of better ways to handle this registration.
            } catch (Exception ex) {
                return (false, $@"Exception: {ex.Message}");
            }
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

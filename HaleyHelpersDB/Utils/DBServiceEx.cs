using Haley.Abstractions;
using Haley.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Haley.Utils {
    public class DBServiceEx : DBService, IDBServiceEx {
        ILogger _logger;
        ConcurrentDictionary<Type, IDBModule> _dic = new ConcurrentDictionary<Type, IDBModule>();

        ConcurrentDictionary<Type, string> _moduleKeys = new ConcurrentDictionary<Type, string>();

        string _defaultAdapterKey = string.Empty;

        public void SetDefaultAdapterKey(string adapterKey) {
            //Set the adapter key for all the modules (if not provided along with the parameter)
            if (string.IsNullOrWhiteSpace(adapterKey)) throw new ArgumentNullException(nameof(adapterKey));
            _defaultAdapterKey = adapterKey;
        }
        public void SetDefaultAdapterKey<P>(string adapterKey) 
            where P : IModuleParameter {
            if (string.IsNullOrWhiteSpace(adapterKey)) throw new ArgumentNullException(nameof(adapterKey));
            //Set default adapter key for the specific module, if not provided along with the parameter.
            if (!_moduleKeys.ContainsKey(typeof(P))) {
                _moduleKeys.TryAdd(typeof(P), adapterKey);
            } else {
                _moduleKeys[typeof(P)] = adapterKey;
            }
        }
        public Task<IFeedback> TryRegisterModule<M>()
         where M : class, IDBModule, new() {
            return TryRegisterModule<M>(null);
        }
        public Task<IFeedback> TryRegisterModule<M>(Dictionary<string, object> seed)
         where M : class, IDBModule,new() {
            return TryRegisterModule<M>(null, seed);
        }

        //Try to get the dependency injection resolver to build the DBModule? Will it be an overkill??
        //How do we pass the DB Service to the modules? Should we delegate this task to the Module? or is it fine to pass the service from here itself?
        public Task<IFeedback> TryRegisterModule<M>(M module, Dictionary<string, object> seed)
            where M : class, IDBModule //Register a module
        {
            return TryRegisterModuleInternal(typeof(M),module ,seed);
        }
        async Task<IFeedback> TryRegisterModuleInternal(Type mType, IDBModule module, Dictionary<string,object> seed) {
            IFeedback result = new Feedback(false);
            try {
                //First try to see if the Module has a generic parameter, if yes, then focus on getting it else check if the user has defined any parameter type directly.
                var dbmInterface = mType.GetInterfaces()?.FirstOrDefault(p =>
                    p.IsGenericType &&
                    p.Name == $@"{nameof(IDBModule)}`1");

                if (dbmInterface == null) return new Feedback(false, $@"The module should implement the generic interface{nameof(IDBModule)}<> ");

                if (module == null) module = (IDBModule)Activator.CreateInstance(mType);
                Type paramType = dbmInterface.GetGenericArguments().Where(
                    p => p.GetInterfaces().Any(q => q.Name == $@"{nameof(IModuleParameter)}")
                    ).FirstOrDefault() ?? module.ParameterType;
                if (paramType == null) return new Feedback(false, $@"The type argument of {nameof(IDBModule)} should implement {nameof(IModuleParameter)}");
                ////var cmdType = paramType.GetInterfaces()?.FirstOrDefault(p => p.IsGenericType && p.Name == $@"{nameof(IModuleParameter)}`1");
                ////if (cmdType == null) return (false, $@"The type argument of {nameof(IDBModule)} should implement {nameof(IModuleParameter)} ");//Even after above step if we dont' get the parameter type, don't register it.
                if (_dic.ContainsKey(paramType)) return new Feedback(false, $@"{paramType} is already registered.");
                if (seed == null) seed = new Dictionary<string, object>();
                if (!seed.ContainsKey("dbs") || seed["dbs"].GetType().IsAssignableFrom(typeof(IDBService))) {
                    seed.TryAdd("dbs", this);
                }
                if (!seed.ContainsKey("logger") || seed["logger"].GetType().IsAssignableFrom(typeof(ILogger))) {
                    seed.TryAdd("logger", _logger);
                }

                //Reset the module parameter type as well
                if (module is DBModule dbMdl) {
                    dbMdl.SetSeed(seed); //Set the seed only via this service.
                    dbMdl.SetParameterType(paramType);
                    var initializeStats = await dbMdl.Initialize(); //Default module initialization
                    if (!initializeStats.Status) return initializeStats;
                }
                var status = _dic.TryAdd(paramType, module);
                return new Feedback(status, status ? "Success" : "Failed to register the module");
                //todo: think of better ways to handle this registration.
            } catch (Exception ex) {
                return new Feedback(false, $@"Exception: {ex.Message}");
            }
        }
        public Task<IFeedback> Execute<P>(Enum cmd, P arg) where P : IModuleParameter {
            var argT = typeof(P);
            if (!_dic.ContainsKey(argT)) throw new KeyNotFoundException($@"{argT}");
            if (string.IsNullOrWhiteSpace(arg.AdapterKey)) {
                if (_moduleKeys.ContainsKey(typeof(P))) {
                    arg.AdapterKey = _moduleKeys[typeof(P)];
                } else if (!string.IsNullOrWhiteSpace(_defaultAdapterKey)) {
                    arg.AdapterKey = _defaultAdapterKey;
                } else {
                    throw new ArgumentNullException("Cannot execute without a default adapter key.");
                }
            }
            return _dic[argT].Execute(cmd,arg);
        }
        public IFeedback GetCommandStatus<P>(Enum cmd) where P : IModuleParameter {
            var argT = typeof(P);
            if (!_dic.ContainsKey(argT)) return new Feedback(false, $@"{argT} is not registered to any module");
            return _dic[argT].GetInvocationMethodName(cmd);
        }
        public async Task<IFeedback> TryRegisterAssembly(Assembly assembly) {
            List<IFeedback> results = new List<IFeedback>();
            if (assembly == null) return new Feedback(false, "Assembly is null");
            try {
               var targetClasses = assembly.GetExportedTypes()?.Where(p => p.GetCustomAttribute<RegisterDBModuleAttribute>() != null);
                if (targetClasses == null || targetClasses.Count() < 1) return new Feedback(false, $@"Unable to find any class with attribute {nameof(RegisterDBModuleAttribute)} ");
                foreach (var classType in targetClasses) {
                    IFeedback targetfb = new Feedback() {Result = classType.Name };
                    try {
                       targetfb = await TryRegisterModuleInternal(classType,null, null);
                    } catch (Exception ex) {
                        targetfb.Status = false;
                        targetfb.Message = classType.Name + Environment.NewLine + ex.Message;
                    }
                    targetfb.Message = classType.Name; //add the name of the class.
                    results.Add(targetfb);
                }
            } catch (Exception ex) {
                return new Feedback(false, $@"Exception: {ex.Message} ");
            }

            bool regsuccess = results.All(p => p.Status);
            var result = new Feedback(results.All(p => p.Status));
            if (result.Status) {
                result.Message = $@"ASM : {assembly} - Registration completed";
            } else {
                result.Message = $@"ASM : {assembly} - Failed with errors";
            }
            result.Result = results;
            return result;
        }
        public DBServiceEx(ILogger logger, bool autoConfigure = true):base(autoConfigure) {
            _logger = logger;
        }
        public DBServiceEx() : this(null,true) { }
    }
}

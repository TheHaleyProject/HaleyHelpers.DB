using Haley.Abstractions;
using Haley.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Haley.Utils {
    public class DBModuleService : DBService, IDBModuleService {
        ILogger _logger;
        ConcurrentDictionary<Type, IDBModule> _modules = new ConcurrentDictionary<Type, IDBModule>();
        ConcurrentDictionary<Type, string> _moduleAdapterKeys = new ConcurrentDictionary<Type, string>();
        string _defaultAdapterKey = string.Empty;

        public void SetDefaultAdapterKey(string adapterKey) {
            //Set the adapter key for all the modules (if not provided along with the parameter)
            if (string.IsNullOrWhiteSpace(adapterKey)) throw new ArgumentNullException(nameof(adapterKey));
            _defaultAdapterKey = adapterKey;
        }
        public void SetDefaultAdapterKey<P>(string adapterKey) 
            where P : IModuleArgs {
            SetDefaultAdapterKey(typeof(P), adapterKey);
        }
        void SetDefaultAdapterKey(Type moduleType, string adapterKey) {
            if (string.IsNullOrWhiteSpace(adapterKey)) throw new ArgumentNullException(nameof(adapterKey));
            //Set default adapter key for the specific module, if not provided along with the parameter.
            if (!_moduleAdapterKeys.ContainsKey(moduleType)) {
                _moduleAdapterKeys.TryAdd(moduleType, adapterKey);
            } else {
                _moduleAdapterKeys[moduleType] = adapterKey;
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
        async Task<IFeedback> TryRegisterModuleInternal(Type mType, IDBModule module, Dictionary<string,object> seed, string defaultAdapterKey = null) {
            IFeedback result = new Feedback(false);
            try {
                //First try to see if the Module has a generic parameter, if yes, then focus on getting it else check if the user has defined any parameter type directly.
                var dbmInterface = mType.GetInterfaces()?.FirstOrDefault(p =>
                    p.IsGenericType &&
                    p.Name == $@"{nameof(IDBModule)}`1");

                if (dbmInterface == null) return new Feedback(false, $@"The module should implement the generic interface{nameof(IDBModule)}<> ");

                if (module == null) module = (IDBModule)Activator.CreateInstance(mType);
                Type paramType = dbmInterface.GetGenericArguments().Where(
                    p => p.GetInterfaces().Any(q => q.Name == $@"{nameof(IModuleArgs)}")
                    ).FirstOrDefault() ?? module.ParameterType;
                if (paramType == null) return new Feedback(false, $@"The type argument of {nameof(IDBModule)} should implement {nameof(IModuleArgs)}");
                ////var cmdType = paramType.GetInterfaces()?.FirstOrDefault(p => p.IsGenericType && p.Name == $@"{nameof(IModuleParameter)}`1");
                ////if (cmdType == null) return (false, $@"The type argument of {nameof(IDBModule)} should implement {nameof(IModuleParameter)} ");//Even after above step if we dont' get the parameter type, don't register it.
                if (_modules.ContainsKey(paramType)) return new Feedback(false, $@"{paramType} is already registered.");
                if (seed == null) seed = new Dictionary<string, object>();
                if (!seed.ContainsKey("ms") || !seed["ms"].GetType().IsAssignableFrom(typeof(IDBModuleService))) {
                    seed.TryAdd("ms", this);
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
                var status = _modules.TryAdd(paramType, module);
                if (status && !string.IsNullOrWhiteSpace(defaultAdapterKey)) {
                    SetDefaultAdapterKey(paramType, defaultAdapterKey);
                }
                return new Feedback(status, status ? "Success" : "Failed to register the module");
                //todo: think of better ways to handle this registration.
            } catch (Exception ex) {
                return new Feedback(false, $@"Exception: {ex.Message}");
            }
        }
        public IDBModule GetModule<P>() where P : IModuleArgs {
            var argT = typeof(P);
            if (!_modules.ContainsKey(argT)) return null;
            return _modules[argT];
        }
        public string GetAdapterKey<P>() where P : IModuleArgs {
            var argT = typeof(P);
            if (!_moduleAdapterKeys.ContainsKey(argT)) return GetAdapterKey();
            return _moduleAdapterKeys[argT];
        }
        public string GetAdapterKey() {
            return _defaultAdapterKey;
        }
        public IFeedback GetCommandStatus<P>(Enum cmd) where P : IModuleArgs {
            return GetModule<P>()?.GetInvocationMethodName(cmd) ?? (IFeedback)new Feedback(false);
        }
        public ITransactionHandler GetTransactionHandler<P>() where P : IModuleArgs {
           var akey =  GetAdapterKey<P>();
            if (string.IsNullOrWhiteSpace(akey)) throw new ArgumentNullException("Adapter key cannot be null or empty");
            return base.GetTransactionHandler(akey); 
        }
        public async Task<IFeedback> TryRegisterAssembly(Assembly assembly,string defaultAdapterKey = null) {
            List<IFeedback> results = new List<IFeedback>();
            if (assembly == null) return new Feedback(false, "Assembly is null");
            try {
               var targetClasses = assembly.GetExportedTypes()?.Where(p => p.GetCustomAttribute<RegisterDBModuleAttribute>() != null);
                if (targetClasses == null || targetClasses.Count() < 1) return new Feedback(false, $@"Unable to find any class with attribute {nameof(RegisterDBModuleAttribute)} ");
                foreach (var classType in targetClasses) {
                    IFeedback targetfb = new Feedback() {Result = classType.Name };
                    try {
                       targetfb = await TryRegisterModuleInternal(classType,null, null, defaultAdapterKey);
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
        protected override IDBService GetDBService() {
            return this;
        }
        public Task<IFeedback> Execute<P>(P arg) where P : IModuleArgs {
            var argT = typeof(P);
            if (string.IsNullOrWhiteSpace(arg.Key) && arg is ParameterBase pb) {
                if (_moduleAdapterKeys.ContainsKey(argT)) {
                    pb.Key = _moduleAdapterKeys[argT];
                } else if (!string.IsNullOrWhiteSpace(_defaultAdapterKey)) {
                    pb.Key = _defaultAdapterKey;
                } else {
                    throw new ArgumentNullException("Cannot execute without a default adapter key.");
                }
            }
            return GetModule<P>()?.Execute(arg) ?? Task.FromResult((IFeedback)new Feedback(false));
        }
        public DBModuleService(ILogger logger, bool autoConfigure = true):base(autoConfigure) {
            _logger = logger;
        }
        public DBModuleService() : this(null,true) { }
    }
}

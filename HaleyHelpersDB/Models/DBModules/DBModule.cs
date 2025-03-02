﻿using Haley.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace Haley.Models {
    public abstract class DBModule<P> : DBModule, IDBModule<P> where P : IModuleParameter {
        //protected ConcurrentDictionary<Enum,Func<P, Task<DBMResult>>> CmdDic = new ConcurrentDictionary<Enum, Func<P, Task<DBMResult>>>();
        public override async Task<IFeedback> Execute(IModuleParameter parameter) {
            if (parameter == null || parameter.Command == null) return new Feedback(false, "Input parameter and the Command property of Input parameter cannot be null");
            if (!CmdDic.ContainsKey(parameter.Command)) return new Feedback(false, $@"Command {parameter.Command} is not registered.");
            if (!parameter.GetType().IsAssignableFrom(typeof(P))) return new Feedback(false,$@"Input parameter should be of type {typeof(P)}");
            //return await CmdDic[parameter.Command].DynamicInvoke((P)parameter);
            var result = CmdDic[parameter.Command].DynamicInvoke((P)parameter);
            if (result is Task<IFeedback> task) {
                return await task;
            }
            return new Feedback(false, "Unable to invoke the delegate command");
        }
    }

    public abstract class DBModule : IDBModule {
        protected ConcurrentDictionary<Enum, DBMExecuteDelegate> CmdDic = new ConcurrentDictionary<Enum, DBMExecuteDelegate>();
        public abstract Task<IFeedback> Execute(IModuleParameter parameter);
        public Type ParameterType { get; private set; }
        protected Dictionary<string, object> Seed { get; set; } //Either set by inheritance or by internal services
        internal void SetParameterType(Type ptype) => ParameterType = ptype;
        internal void SetSeed(Dictionary<string, object> seed) => Seed = seed ?? new Dictionary<string, object>();
        public event EventHandler<DBModuleInitializedArgs> ModuleInitialized;
        protected IDBService DBS { get; set; }
        protected ILogger Logger { get; set; }
        public bool IsInitialized { get; protected set; }
        protected virtual Task<IFeedback> InitializeInternal() { return Task.FromResult((IFeedback)new Feedback(true)); }
        public IFeedback GetInvocationMethodName(Enum cmd) {
            if (CmdDic.ContainsKey(cmd) && CmdDic[cmd] != null && CmdDic[cmd].Method != null) return new Feedback(true, $@"{CmdDic[cmd].Method.DeclaringType} : {CmdDic[cmd].Method.Name}");
            return new Feedback(false, "Command Not registered");
        }
        public async Task<IFeedback> Initialize() {
            if (IsInitialized) return new Feedback(false,"Module already initialized");
            //During registration, the DBS will be provided by the DBMService as long as the module is inherited from DefaultModule.
            SetServices();
            if (!(await RegisterCommands(GetType())).Status)return new Feedback(false,"Command registrations failed"); //Start with this type
            if (!(await InitializeInternal()).Status) return new Feedback(false,"Internal Initialization failed"); //setup the default values.
            await OnInitialization(); //Do we await?
            return new Feedback(true);
        }

        async Task<IFeedback> RegisterCommands(Type target) {
            //For each type and their base level.
            IFeedback registerfb = new Feedback();
            bool registered = false;
            while (!registered) {
                var methods = target
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<CMDAttribute>() != null); //Let us focus only on the private methods.
                foreach (var method in methods) {
                    try {
                        var cmdattr = method.GetCustomAttribute<CMDAttribute>();
                        if (cmdattr.Name == null || !(cmdattr.Name is Enum @cmd)) throw new Exception($@"Registration Failed: {method.DeclaringType} : {method.Name} -- {nameof(CMDAttribute)} should have a name of type {nameof(Enum)}");

                        if (method.ReturnType != typeof(Task<IFeedback>)) throw new Exception($@"Registration Failed: {method.DeclaringType} : {method.Name} --  Return type doesn't match {nameof(Task<IFeedback>)}");

                        var inParams = method.GetParameters();
                        if (inParams == null || inParams[0] == null || !inParams[0].ParameterType.IsAssignableFrom(typeof(IModuleParameter))) throw new Exception($@"Registration Failed: {method.DeclaringType} : {method.Name} --  Signature doesn't match the type {nameof(IModuleParameter)}");

                        //Instead of storing as MethodInfo, it is better to generate the delegate and call this, as the overhead and reflection time is less during runtime.
                        if (CmdDic.ContainsKey(@cmd)) throw new Exception($@"Failed to register command : {@cmd} for method {method.DeclaringType}-{method.Name}. The command is already registered to method {CmdDic[@cmd].Method}");
                        CmdDic.TryAdd(@cmd, (DBMExecuteDelegate)Delegate.CreateDelegate(typeof(DBMExecuteDelegate), this, method.Name));
                    } catch (Exception ex) {
                        Logger?.LogError(ex.Message);
                        throw; //So that we know that it is not registered.
                    }
                }
                if (target.BaseType != null) {
                    registered = (await RegisterCommands(target.BaseType)).Status;
                } else {
                    registered = true;
                }
            }
            return new Feedback(registered);
        }

        void SetServices() {
            //Since this is a virtual task, there is no guarantee that the initialization willb e completed here itself. It might take more steps to complete. So, donot set initialized here.
            if (Seed == null) throw new Exception($@"Seed cannot be empty. It needs to contain at the least, {nameof(IDBService)} mapped to key : dbs");
            if (Seed.ContainsKey("dbs") && Seed["dbs"] is IDBService _dbs) {
                DBS = _dbs;
            } else {
                throw new Exception("DB Service missing. Seed should contain IDBService against the key : dbs");
            }

            if (Seed.ContainsKey("logger") && Seed["logger"] is ILogger logger) {
                Logger = logger;
            }
        }

        protected virtual Task OnInitialization() {
            IsInitialized = true;  //As this method is called only after initialization, it is safe to set the property here. 
            if (ModuleInitialized != null) {
                ModuleInitialized?.BeginInvoke(this, new DBModuleInitializedArgs(), (a) => { }, null);
            }
            return Task.CompletedTask;
        }
    }
}

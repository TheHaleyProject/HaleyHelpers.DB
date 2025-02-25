using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {
    public abstract class DefaultModule<P> : DefaultModule, IDBModule<P> where P : ModuleParam {
    }

    public abstract class DefaultModule : IDBModule {
        public Type ParameterType { get; internal set; }
        protected  Dictionary<string,object> Seed { get; set; }
        public event EventHandler<DBModuleInitializedArgs> ModuleInitialized;
        public abstract Task<object> Execute(ModuleParam parameter);
        protected IDBService DBS { get; set; }
        public bool IsInitialized { get; protected set; }
        protected virtual Task Initialize(Dictionary<string,object> seed) {
            if (seed != null && seed.ContainsKey("dbs") && seed["dbs"] is IDBService _dbs) {
                DBS = _dbs;
            }
            Seed = seed; //store it for future reference and use.
            return Task.CompletedTask;
        }
        protected virtual Task OnInitialization() {
            if (ModuleInitialized != null) {
                ModuleInitialized?.BeginInvoke(this, new DBModuleInitializedArgs(), (a) => { }, null);
            }
            return Task.CompletedTask;
        }
    }
}

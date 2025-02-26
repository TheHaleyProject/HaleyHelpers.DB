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
        protected  Dictionary<string,object> Seed { get; set; } //Either set by inheritance or by internal services
        internal void SetSeed(Dictionary<string, object> seed) => Seed = seed ?? new Dictionary<string, object>();
        public event EventHandler<DBModuleInitializedArgs> ModuleInitialized;
        public abstract Task<object> Execute(ModuleParam parameter);
        protected IDBService DBS { get; set; }
        public bool IsInitialized { get; protected set; }
        public virtual Task<bool>  Initialize() {
            if (IsInitialized) return Task.FromResult(false);
            //Since this is a virtual task, there is no guarantee that the initialization willb e completed here itself. It might take more steps to complete. So, donot set initialized here.
            if (Seed != null && Seed.ContainsKey("dbs") && Seed["dbs"] is IDBService _dbs) {
                DBS = _dbs;
            }
            return Task.FromResult(true);
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

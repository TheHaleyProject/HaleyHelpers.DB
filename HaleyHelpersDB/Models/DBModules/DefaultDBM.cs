using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {
    public abstract class DefaultDBM<P> : DefaultDBM, IDBModule<P> where P : IModuleParameter {
       
    }

    public abstract class DefaultDBM : IDBModule {
        public abstract Task<object> Execute(IModuleParameter parameter);
        public Type ParameterType { get; internal set; }
        protected Dictionary<string, object> Seed { get; set; } //Either set by inheritance or by internal services
        internal void SetSeed(Dictionary<string, object> seed) => Seed = seed ?? new Dictionary<string, object>();
        public event EventHandler<DBModuleInitializedArgs> ModuleInitialized;
        protected IDBService DBS { get; set; }
        public bool IsInitialized { get; protected set; }
        protected abstract Task<bool> InitializeInternal();
        public async Task<bool> Initialize() {
            //During registration, the DBS will be provided by the DBMService as long as the module is inherited from DefaultModule.
            SetDBS();
            if (!await InitializeInternal()) return false; //setup the default values.
            await OnInitialization(); //Do we await?
            return true;
        }

        void SetDBS() {
            if (IsInitialized) throw new Exception("Module already initialized");
            //Since this is a virtual task, there is no guarantee that the initialization willb e completed here itself. It might take more steps to complete. So, donot set initialized here.
            if (Seed != null && Seed.ContainsKey("dbs") && Seed["dbs"] is IDBService _dbs) {
                DBS = _dbs;
            } else {
                throw new Exception("DB Service missing. Seed should contain IDBService against the key : dbs");
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

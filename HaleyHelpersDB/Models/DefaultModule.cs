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
        public abstract Task<object> Execute(ModuleParam parameter);
        public virtual Task PostInitialization(ModuleSeed seed) { return Task.CompletedTask; }
        public IDBService Dbs { get; internal set; }
        public bool IsInitialized { get; protected set; }
        protected abstract Task Initialize();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haley.Enums;
using Haley.Models;

namespace Haley.Abstractions {
    public interface IDBModuleService : IDictionary<string,IDBModule> {
        public IDBService DBService { get; }
        public IDBModule this[Enum key] { get;set; }
        public bool ContainsKey(Enum key);
       
        public bool TryRegisterModule<M>(ModuleInfo<M> info)
            where M : IDBModule; //Register a module
    }
}

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
        public bool TryRegisterModule<M>(Enum key, M module)
            where M : IDBModule; //Register a module
        public bool TryRegisterModule<M>(string key, M module)
            where M : IDBModule; //Register a module
    }
}

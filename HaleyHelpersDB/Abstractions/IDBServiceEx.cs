using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haley.Enums;
using Haley.Models;

namespace Haley.Abstractions {
    public interface IDBServiceEx : IDBService{
        public Task<DBMResult> TryRegisterModule<M>()
           where M : IDBModule, new(); //Register a module
        public Task<DBMResult> TryRegisterModule<M>(Dictionary<string, object> seed)
           where M : IDBModule, new(); //Register a module
        public Task<DBMResult> TryRegisterModule<M>(M module, Dictionary<string, object> seed)
            where M : IDBModule; //Register a module
        public Task<DBMResult> Execute<P>(P arg) where P : IModuleParameter;
        public (bool status,string methodName) GetCommandStatus<P>(Enum cmd) where P : IModuleParameter;
    }
}
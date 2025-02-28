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
        public Task<(bool status, string msg)> TryRegisterModule<M>()
           where M : IDBModule, new(); //Register a module
        public Task<(bool status, string msg)> TryRegisterModule<M>(Dictionary<string, object> seed)
           where M : IDBModule, new(); //Register a module
        public Task<(bool status, string msg)> TryRegisterModule<M>(M module, Dictionary<string, object> seed)
            where M : IDBModule; //Register a module
        public Task<(bool status,object result)> Execute<P>(P arg) where P : IModuleParameter;
    }
}
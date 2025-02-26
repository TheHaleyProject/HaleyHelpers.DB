using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haley.Enums;
using Haley.Models;

namespace Haley.Abstractions {
    public interface IDBModuleService{
        public Task<bool> TryRegisterModule<M,P>(M module, Dictionary<string, object> seed)
            where P: DBArg
            where M : IDBModule<P>; //Register a module
        public Task<object> Execute<P>(P arg) where P : DBArg;
    }
}

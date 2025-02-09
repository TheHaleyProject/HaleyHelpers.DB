using Haley.Abstractions;
using Haley.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Utils {
    public class DBMService : ConcurrentDictionary<string, IDBModule>, IDBModuleService {

        //Try to get the dependency injection resolver to build the DBModule? Will it be an overkill??
        public bool TryRegisterModule<M>(Enum key, M module) where M : IDBModule {
            return TryRegisterModule(key.GetKey(),module);
        }

        public bool TryRegisterModule<M>(string key, M module) where M : IDBModule {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (this.ContainsKey(key)) throw new InvalidDataException($@"Key {key} already exists.");
            return this.TryAdd(key, module);
        }
    }
}

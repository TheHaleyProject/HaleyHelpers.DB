using Haley.Abstractions;
using Haley.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Abstractions {
    public interface IDBModule<P> : IDBModule
        where P : DBArg {
    }

    public interface IDBModule {
        Task<object> Execute(DBArg parameter); //Just to enable storing in a common dictionary
        event EventHandler<DBModuleInitializedArgs> ModuleInitialized;
        Task<bool> Initialize(); //will happen only once. //Why is this needed?
        bool IsInitialized { get; }
        Type ParameterType { get; }
    }
}

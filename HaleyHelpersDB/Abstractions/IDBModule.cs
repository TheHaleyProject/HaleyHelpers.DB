﻿using Haley.Abstractions;
using Haley.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Abstractions {
    public interface IDBModule {
        bool IsInitialized { get; }
        Type ParameterType { get; }
        Task<object> Execute(ModuleParam parameter);
        event EventHandler<DBModuleInitializedArgs> ModuleInitialized;
    }

    public interface IDBModule<P> : IDBModule
        where P : ModuleParam {
    }
}

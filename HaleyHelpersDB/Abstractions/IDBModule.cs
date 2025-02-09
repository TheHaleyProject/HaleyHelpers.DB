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
        Type ParameterType { get; }
        Task<object> Execute(ModuleParam parameter);

        Task Initiate(ModuleSeed seed);

    }

    public interface IDBModule<P> : IDBModule
        where P : ModuleParam {
    }
}

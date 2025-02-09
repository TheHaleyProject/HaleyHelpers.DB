using Haley.Abstractions;
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
        Task<object> Execute(object parameter);

        Task Initiate(object seed);

    }

    public interface IDBModule<P> : IDBModule
        where P : ModuleParam {
        Task<object> Execute(P parameter);
    }
}

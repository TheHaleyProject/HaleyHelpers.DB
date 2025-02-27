using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Abstractions{
    public interface IModuleParameter<E> : IModuleParameter
        where E : Enum {
        public E Command { get; set; }
    }

    public interface IModuleParameter {
    }
}
using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models
{
    public delegate Task<Feedback> DBMExecuteDelegate(IModuleParameter parameter);
}

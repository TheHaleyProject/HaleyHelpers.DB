﻿using Haley.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Abstractions{
    public interface IModuleParameter {
        public Enum Command { get; set; }
    }
}
using Haley.Abstractions;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Utils
{
    public static class DBModuleExtensions{
        public static IAdapterParameter Convert(this IParameterBase input) {
            return input.Convert(string.Empty);
        }
        public static IAdapterParameter Convert(this IParameterBase input, string query) {
            if (input == null) throw new ArgumentNullException($@"Input cannot be null for conversion");
            var db = new AdapterParameter(input.Key) { Query = query};
            if (input is ModuleParameter mdp) db.Adapter = mdp.Adapter; //set the target
            return db;
        }
    }
}

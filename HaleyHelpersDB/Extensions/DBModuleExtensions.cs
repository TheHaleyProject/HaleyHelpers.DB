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
        public static IDBInput Convert(this IModuleParameter input) {
            return input.Convert(string.Empty);
        }
        public static IDBInput Convert(this IModuleParameter input, string query) {
            if (input == null) throw new ArgumentNullException($@"Input cannot be null for conversion");
            var db = new DBSInput(input.AdapterKey) { Query = query};
            return db;
        }
    }
}

using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Utils
{
    public static class DBExtensions{
        public static IAdapterParameter Convert(this IParameterBase input) {
            return input.Convert(string.Empty);
        }
        public static IAdapterParameter Convert(this IParameterBase input, string query) {
            if (input == null) throw new ArgumentNullException($@"Input cannot be null for conversion");
            var db = new AdapterParameter(input.Key) { Query = query};
            db.SetParameters(new Dictionary<string, object>(input.Parameters)); //since parameter set is protected.
            if (input is DBModuleInput mdp) {
                db.Adapter = mdp.Adapter; //set the target
                db.TransactionMode = mdp.TransactionMode;
            }
            return db;
        }

        public static IAdapterParameter Add(this IAdapterParameter input, ResultFilter filter) {
            if (input == null) throw new ArgumentNullException($@"Input Parameter cannot be null");
            input.Filter = filter;
            return input;
        }

        public static P ForHandler<P>(this IDBModuleInput input, ITransactionHandler handler) where P: IDBModuleInput {
            return (P)handler.CreateDBInput(input);
        }
    }
}

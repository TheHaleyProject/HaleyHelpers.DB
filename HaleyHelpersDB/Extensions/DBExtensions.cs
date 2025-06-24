using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Utils
{
    public static class DBExtensions{
        public static IAdapterArgs Convert(this IParameterBase input) {
            return input.Convert(string.Empty);
        }
        public static IAdapterArgs Convert(this IParameterBase input, string query) {
            return input.Convert(query, string.Empty);
        }
        public static IAdapterArgs Convert(this IParameterBase input, string query,string groupKey) {
            if (input == null) throw new ArgumentNullException($@"Input cannot be null for conversion");
            var db = new AdapterArgs(input.Key) { Query = query};

            db.SetParameters(new Dictionary<string, object>(string.IsNullOrWhiteSpace(groupKey) ? input.Parameters : input.GetGroupParameters(groupKey))); //since parameter set is protected.

            if (input is ModuleArgs mdp) {
                db.Adapter = mdp.Adapter; //set the target
                db.TransactionMode = mdp.TransactionMode;
            }
            return db;
        }

        public static IAdapterConfig AsAdapterConfig(this string input) {
            if (string.IsNullOrWhiteSpace(input)) return null;
            AdapterConfig result = new AdapterConfig();
            
            return result;
        }

        public static IAdapterArgs Add(this IAdapterArgs input, ResultFilter filter) {
            if (input == null) throw new ArgumentNullException($@"Input Parameter cannot be null");
            input.Filter = filter;
            return input;
        }

        public static P ForTransaction<P>(this IModuleArgs input, ITransactionHandler handler) where P: IModuleArgs {
            return (P)ForTransaction(input, handler);
        }
        public static IModuleArgs ForTransaction(this IModuleArgs input, ITransactionHandler handler) {
            return handler.CreateDBInput(input);
        }
    }
}

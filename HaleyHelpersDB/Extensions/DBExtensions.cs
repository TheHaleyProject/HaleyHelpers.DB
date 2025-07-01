using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Utils
{
    public static class DBExtensions{
        public static IAdapterArgs SetFilter(this IAdapterArgs args,ResultFilter filter) {
            if (args == null) return args;
            args.Filter = filter;
            return args;
        }

        public static IAdapterArgs SetOutputName(this IAdapterArgs args, string output_name) {
            if (args == null) return args;
            args.OutputName = output_name;
            return args;
        }

        public static IAdapterArgs ToAdapterArgs(this Dictionary<string,object> input,string adapterKey, string query) {
            if (input == null || input.Count == 0) throw new ArgumentNullException($@"Input cannot be null or empty for conversion");
            var db = new AdapterArgs(adapterKey) { Query = query };
            db.SetParameters(input);
            return db;
        }

        public static IModuleArgs ToModuleArgs(this Dictionary<string, object> input) {
            if (input == null || input.Count == 0) throw new ArgumentNullException($@"Input cannot be null or empty for conversion");
            var db = new ModuleArgs().SetParameters(input);
            return db;
        }

        public static IAdapterArgs ToAdapterArgs(this IParameterBase input) {
            return input.ToAdapterArgs(string.Empty);
        }
        public static IAdapterArgs ToAdapterArgs(this IParameterBase input, string query) {
            return input.ToAdapterArgs(query, string.Empty);
        }
        public static IAdapterArgs ToAdapterArgs(this IParameterBase input, string query,string groupKey) {
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

        public static P ForTransaction<P>(this IModuleArgs input, ITransactionHandler handler) where P: IModuleArgs {
            return (P)ForTransaction(input, handler);
        }
        public static IModuleArgs ForTransaction(this IModuleArgs input, ITransactionHandler handler) {
            return handler.CreateDBInput(input);
        }
    }
}

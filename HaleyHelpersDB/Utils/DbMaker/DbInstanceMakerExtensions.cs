using Haley.Abstractions;
using Haley.Enums;
using Haley.Internal;
using Haley.Models;
using Haley.Services;
using Haley.Utils;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Utils {
    public static class DbInstanceMakerExtensions {
        static async Task<IFeedback<string>> InitializeWithConString(this DbInstanceMaker input,  IAdapterGateway agw) {
            var result = new Feedback<string>();
            var adapterKey = RandomUtils.GetString(128).SanitizeBase64();
            agw.Add(new AdapterConfig() { 
                AdapterKey = adapterKey,
                ConnectionString = input.ConnectionString,
                DBType = TargetDB.maria
            });

            //input.AdapterKey = adapterKey; // Do not try to replace the existing key because we would later need to use the original key (if present) and try that one incase of failure with connection string.
            var fb = await input.InitializeWithAdapter(agw,adapterKey);
            return result.SetStatus(fb.Status).SetResult(adapterKey);
        }

        static Task<IFeedback> InitializeWithAdapter(this DbInstanceMaker input, IAdapterGateway agw, string? adapterKey = null) {
            //var toReplace = new Dictionary<string, string> { ["lifecycle_state"] = }
            return agw.CreateDatabase(new DbCreationArgs(adapterKey ?? input.AdapterKey) {
                ContentProcessor = (content, dbname) => {
                    //Custom processor to set the DB name in the SQL content.
                    return content.Replace(input.ReplaceDbName, dbname);
                },
                FallBackDBName = input.FallbackDbName,
                SQLContent = input.SqlContent,
            });
        }

        #region Wrapper making 
        public static DbInstanceMaker WithConnectionString(this DbInstanceMaker input,string con_string) {
            input.ConnectionString = con_string;
            return input;
        }
        public static DbInstanceMaker WithAdapterKey(this DbInstanceMaker input, string adapterKey) {
            input.AdapterKey = adapterKey;
            return input;
        }
        public static DbInstanceMaker WithProvider(this DbInstanceMaker input, IServiceProvider provider) {
            input.ServiceProvider = provider;
            return input;
        }

        #endregion
        public static async Task<string> Initialize(this DbInstanceMaker input, IAdapterGateway agw) {

            if (input == null) throw new ArgumentException(nameof(input));
            bool isInitialized = false;
            string adapterKey = string.Empty;
            string errMessage = string.Empty;

            //DB Initialization
            do {
                //Try initialization with Connection string
                if (!string.IsNullOrWhiteSpace(input.ConnectionString)) {
                    var conResponse = await input.InitializeWithConString(agw);
                    if (conResponse != null && conResponse.Status && conResponse.Result != null) {
                        adapterKey = conResponse.Result;
                    } else {
                        errMessage = conResponse?.Message;
                    }
                }
                if (!string.IsNullOrWhiteSpace(adapterKey)) break; //We hvae a key, go ahead.
                if (string.IsNullOrWhiteSpace(input.AdapterKey)) break; //We dont have a key but also, dont have adapterkey from input.

                //Try with Adapter key
                var fb = await input.InitializeWithAdapter(agw);
                if (fb != null && fb.Status) {
                    adapterKey = input.AdapterKey;
                } else {
                    errMessage = fb?.Message;
                }

            } while (false);

            if (string.IsNullOrWhiteSpace(adapterKey)) throw new ArgumentException($@"Unable to initialize the database for {input.ReplaceDbName}. {errMessage}");
            return adapterKey;
        }
    }
}

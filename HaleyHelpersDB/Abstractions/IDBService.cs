using Haley.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Haley.Enums;

namespace Haley.Abstractions {
    public interface IDBService: IDictionary<string, DBAdapter> {
        //This should be stateless as every controller might call this concurrently.
        public Task<object> Read(string dba_key, ILogger logger, string query, ResultFilter filter = ResultFilter.FirstDictionaryValue, params (string key, object value)[] parameters);

        public Task<object> NonQuery(string dba_key, ILogger logger, string query, ResultFilter filter = ResultFilter.FirstDictionaryValue, params (string key, object value)[] parameters);
        public void SetServiceUtil(IDBServiceUtil util);
    }
}

using Haley.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Haley.Enums;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace Haley.Abstractions {
    public interface IDBService: IDictionary<string, DBAdapter> {
        //This should be stateless as every controller might call this concurrently.
        public Task<object> Read(DBInput input, params (string key, object value)[] parameters);
        public Task<object> NonQuery(DBInput input,  params (string key, object value)[] parameters);
        public void SetServiceUtil(IDBServiceUtil util);
        public Task<object> GetFirst(object input, ResultFilter filter = ResultFilter.None);
        public IConfigurationRoot GetConfigurationRoot(bool reload = false);
        public IDBService UpdateAdapter();
        public IDBService Configure();
        public string GetSchemaName(string dba_key);
    }
}

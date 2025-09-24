using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Microsoft.Extensions.Logging;
using System.Collections;
using Haley.Utils;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Haley.Enums;
using System.Reflection.Metadata.Ecma335;
using Haley.Models;

namespace Haley.Utils {
    public static class DataSetExtensions {
        public static List<Dictionary<string, object>> Convert(this DataTable dt, ILogger logger, bool handleJson = false) {
            try {
                return dt.Convert(handleJson).ToList();
            } catch (Exception ex) {
                logger?.LogError(ex.ToString());
                return null;
            }
        }

        public static DataTable Select(this DataSet ds,int index) {
            var count = ds?.Tables.Count - 1; //We are already considering the -1
            if (ds == null || count == null || count.Value < 0) return null;
            if (index > count.Value || index < 0) index = count.Value; // In these cases, go for the last table.
            return ds.Tables[index];
        }
        public static DataTable Select(this DataSet ds, bool islast) {
            return ds.Select(islast ? -1 : 0); // for last select -1, else select the first one.
        }
    }
}

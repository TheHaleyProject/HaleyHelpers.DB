using Haley.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {

    public class DbaEntry {

        public DbaEntry() {
            DBType = TargetDB.unknown;
        }
        public string ConnectionString { get; set; }
        public string AdapterKey { get; set; }
        public string ConnectionKey { get; set; }
        public string DBName { get; set; }
        public TargetDB DBType { get; set; }
        public string SchemaName { get; set; }
        public string Sha { get; set; }

        public DbaEntry Update(DbaEntry entry) {
            //It is intentional not to update the ConnetionKey and AdapterKey.

            DBName = entry.DBName;
            ConnectionString = entry.ConnectionString;
            DBType = entry.DBType;
            SchemaName = entry.SchemaName;  
            Sha = entry.Sha;
            return this;
        }
    }
}
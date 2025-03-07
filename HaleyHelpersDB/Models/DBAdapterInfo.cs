using Haley.Abstractions;
using Haley.Enums;

namespace Haley.Models {

    public class DBAdapterInfo : IDBAdapterInfo {

        public DBAdapterInfo() {
            DBType = TargetDB.unknown;
        }
        public string ConnectionString { get; set; }
        public string AdapterKey { get; set; }
        public string ConnectionKey { get; set; }
        public string DBName { get; set; }
        public TargetDB DBType { get; set; }
        public string SchemaName { get; set; }
        public string Sha { get; set; }

        public object Clone() {
            return new DBAdapterInfo() {
                AdapterKey = this.AdapterKey,
                ConnectionKey = this.ConnectionKey,
                ConnectionString = this.ConnectionString,
                DBName = this.DBName,
                DBType = this.DBType,
                SchemaName = this.SchemaName,
                Sha = this.Sha
            };
        }

        public IDBAdapterInfo Update(IDBAdapterInfo entry) {
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
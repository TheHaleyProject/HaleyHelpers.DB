using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class DBAdapterEx : DBAdapter, IDBAdapterEx {
        public void Dispose() {
            throw new NotImplementedException();
        }

        public DBAdapterEx(IDBAdapterInfo entry): base(entry) {
            //We are generating a new adapter which is disposable.
        }
    }
}

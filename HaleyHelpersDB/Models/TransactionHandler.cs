using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public sealed class TransactionHandler : DBAdapter, ITransactionHandler {
        //This is nothing but a proxy, which also allows DBAdapter calls.
        public void Dispose() => Handler.Dispose();
        public Task Commit() => Handler.Commit();
        public Task Rollback() => Handler.Rollback();
        public async Task<IDBTransaction> Begin() {
             await Handler.Begin(); //Do not return this object.
            return this; //Send this as the transaction.
        }
        public TransactionHandler(IDBAdapterInfo entry, bool transactionMode): base(entry,transactionMode) {
        }
    }
}

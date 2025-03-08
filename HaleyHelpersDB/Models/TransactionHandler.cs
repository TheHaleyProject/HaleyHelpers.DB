using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public sealed class TransactionHandler : DBAdapter, ITransactionHandler {
        public void Dispose() {
            Handler.Dispose();
        }

        public async Task<IDBTransaction> Start() {
             await Handler.Start();
            return this; //Send this as the transaction.
        }

        public Task Commit() {
            return Handler.Commit();
        }

        public Task Rollback() {
            return Handler.Commit();
        }

        public TransactionHandler(IDBAdapterInfo entry, bool transactionMode): base(entry,transactionMode) {
        }
    }
}

using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public sealed class TransactionHandler : DBAdapter, ITransactionHandler {

        internal IDBModuleService Dbm { get; set; }
        //This is nothing but a proxy, which also allows DBAdapter calls.
        public void Dispose() => Handler.Dispose();
        public Task Commit() => Handler.Commit();
        public Task Rollback() => Handler.Rollback();
        public async Task<IDBTransaction> Begin() {
             await Handler.Begin(); //Do not return this object.
            return this; //Send this as the transaction.
        }

        public Task<IFeedback> Execute<P>(Enum cmd, P arg) where P : IModuleParameter {
            if (Dbm == null) throw new ArgumentNullException($@"DBModule service is not defined inside the Transaction Handler.");

        }

        public IFeedback GetCommandStatus<P>(Enum cmd) where P : IModuleParameter {
            if (Dbm == null) throw new ArgumentNullException($@"DBModule service is not defined inside the Transaction Handler.");
        }

        public TransactionHandler(IDBAdapterInfo entry, bool transactionMode): base(entry,transactionMode) {
        }
    }
}

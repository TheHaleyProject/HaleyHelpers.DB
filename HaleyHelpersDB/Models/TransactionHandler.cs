using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public sealed class TransactionHandler : DBAdapter, ITransactionHandler {
        IDBService _dbsVar;
        internal IDBService _dbs { 
            get { return _dbsVar; }
            set {
                _dbsVar = value;
                if(value != null && value.GetType().GetInterfaces().Any(p=> p == typeof(IDBModuleService))){
                    //if dbs is also a db module service, 
                    _dbms = (IDBModuleService)value;
                }}
        } 

        IDBModuleService _dbms;
        //This is nothing but a proxy, which also allows DBAdapter calls.
        public void Dispose() => SQLHandler.Dispose();
        public Task Commit() => SQLHandler.Commit();
        public Task Rollback() => SQLHandler.Rollback();
        public async Task<IDBTransaction> Begin() {
             await SQLHandler.Begin(); //Do not return this object.
            return this; //Send this as the transaction.
        }

        void ValidateDBService(bool validateModule = false) {
            if (_dbs == null) throw new ArgumentNullException($@"DBService is not defined inside the Transaction Handler for executing this operation.");
            if (validateModule && _dbms == null) throw new ArgumentException($@"DB Module Service is not defined inside the Transaction Handler for executing this operation.");
        }

        public Task<IFeedback> Execute<P>(Enum cmd, P arg) where P : IParameterBase {
            ValidateDBService();
            //Now, we need to attach the adapter to the argument.
            if (arg != null && arg is ModuleParameter argMP) argMP.Adapter = this;
            //if required, we can also fetch the key and set here itself.
            arg.Key = _dbms.GetModuleKey<P>();
            return _dbms.GetModule<P>().Execute(cmd, arg);
        }

        public IFeedback GetCommandStatus<P>(Enum cmd) where P : IParameterBase {
            ValidateDBService();
            return _dbms.GetCommandStatus<P>(cmd);
        }

        public TransactionHandler(IDBAdapterInfo entry): base(entry) {
        }
    }
}

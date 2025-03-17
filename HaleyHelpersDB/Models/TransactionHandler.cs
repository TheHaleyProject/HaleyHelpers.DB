using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

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
        public IFeedback GetCommandStatus<P>(Enum cmd) where P : IDBModuleInput {
            ValidateDBService();
            return _dbms.GetCommandStatus<P>(cmd);
        }
        public IDBModule GetModule<P>() where P : IDBModuleInput {
            ValidateDBService();
            return _dbms.GetModule<P>();
        }
        public string GetModuleKey<P>() where P : IDBModuleInput {
            ValidateDBService();
            return _dbms.GetModuleKey<P>();
        }
        public string GetAssemblyKey<P>() where P : Assembly {
            ValidateDBService();
            return _dbms.GetAssemblyKey<P>();
        }
        public Task<IFeedback> Execute<P>(P arg) where P : IDBModuleInput {
            ValidateDBService();
            //Now, we need to attach the adapter to the argument.
            if (arg != null && arg is DBModuleInput argMP) {
                argMP.Adapter = this;
                argMP.Key = _dbms.GetModuleKey<P>();
            }
            //if required, we can also fetch the key and set here itself.
            return _dbms.GetModule<P>().Execute(arg);
        }

       

        public TransactionHandler(IDBAdapterInfo entry): base(entry) {
        }
    }
}

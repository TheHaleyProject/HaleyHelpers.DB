using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class TransactionHandler : DBAdapter, ITransactionHandler {
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
        //public void Dispose() { }
        public void Commit() => SQLHandler.Commit();
        public void Rollback() => SQLHandler.Rollback();
        public IDBTransaction Begin() {
            SQLHandler.Begin(); //Do not return this object.
            return this; //Send this as the transaction.
        }
        public P CreateDBInput<P>() where P: IModuleArgs,new() {
            return CreateDBInput(new P());
        }

        public P CreateDBInput<P>(P arg) where P : IModuleArgs {
            if (arg != null && arg is ModuleArgs argMP) {
                argMP.Adapter = this;
                argMP.TransactionMode = true;
            }
            return arg;
        }
        void ValidateDBService(bool validateModule = false) {
            if (_dbs == null) throw new ArgumentNullException($@"DBService is not defined inside the Transaction Handler for executing this operation.");
            if (validateModule && _dbms == null) throw new ArgumentException($@"DB Module Service is not defined inside the Transaction Handler for executing this operation.");
        }
        public IFeedback GetCommandStatus<P>(Enum cmd) where P : IModuleArgs {
            ValidateDBService();
            return _dbms.GetCommandStatus<P>(cmd);
        }
        public IDBModule GetModule<P>() where P : IModuleArgs {
            ValidateDBService();
            return _dbms.GetModule<P>();
        }
        public string GetAdapterKey<P>() where P : IModuleArgs {
            ValidateDBService();
            return _dbms.GetAdapterKey<P>();
        }
        public string GetAdapterKey() {
            ValidateDBService();
            return _dbms.GetAdapterKey();
        }
        public Task<IFeedback> Execute<P>(P arg) where P : IModuleArgs {
            ValidateDBService();
            //Now, we need to attach the adapter to the argument.
            if (arg != null && arg is ModuleArgs argMP) {
                argMP.Adapter = this;
                argMP.Key = _dbms.GetAdapterKey<P>();
                argMP.TransactionMode = true; //not required at all
            }
            //if required, we can also fetch the key and set here itself.
            return _dbms.GetModule<P>().Execute(arg);
        }

        public TransactionHandler(IAdapterConfig entry): base(entry) {
        }
    }
}

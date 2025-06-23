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
        public IModuleArgs CreateDBInput(Enum cmd) {
            return CreateDBInput(cmd, new ModuleArgs());
        }

        public IModuleArgs CreateDBInput(Enum cmd, IParameterBase arg) {
            if (arg != null && arg is ModuleArgs argMP) {
                argMP.Adapter = this; //Main purpose is to send this adapter to the executors.
                argMP.TransactionMode = true;
                return argMP;
            }
            return default(IModuleArgs);
        }
        void ValidateDBService(bool validateModule = false) {
            if (_dbs == null) throw new ArgumentNullException($@"DBService is not defined inside the Transaction Handler for executing this operation.");
            if (validateModule && _dbms == null) throw new ArgumentException($@"DB Module Service is not defined inside the Transaction Handler for executing this operation.");
        }
        public IFeedback GetCommandStatus(Enum cmd)  {
            ValidateDBService();
            return _dbms.GetCommandStatus(cmd);
        }
        public IDBModule GetModule<E>() where E : Enum {
            ValidateDBService();
            return _dbms.GetModule<E>();
        }
        public string GetAdapterKey<E>() where E : Enum {
            ValidateDBService();
            return _dbms.GetAdapterKey<E>();
        }

        public IDBModule GetModule(Type enumType){
            ValidateDBService();
            return _dbms.GetModule(enumType);
        }
        public string GetAdapterKey(Type enumType){
            ValidateDBService();
            return _dbms.GetAdapterKey(enumType);
        }
        public string GetAdapterKey() {
            ValidateDBService();
            return _dbms.GetAdapterKey();
        }

        public Task<IFeedback> Execute(Enum cmd, IParameterBase arg) {
            ValidateDBService();
            //Now, we need to attach the adapter to the argument.
            if (arg != null && arg is ModuleArgs argMP) {
                argMP.Adapter = this;
                argMP.Key = _dbms.GetAdapterKey(cmd.GetType());
                argMP.TransactionMode = true; //not required at all
            }
            //if required, we can also fetch the key and set here itself.
            return _dbms.GetModule(cmd.GetType()).Execute(cmd,arg as IModuleArgs);
        }

        public Task<IFeedback> Execute(Enum cmd) {
           return Execute(cmd, new ModuleArgs());
        }

        public TransactionHandler(IAdapterConfig entry): base(entry) {
        }
    }
}

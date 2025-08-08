using Haley.Abstractions;
using Haley.Enums;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class TransactionHandler : DBAdapter, ITransactionHandler {
        IAdapterGateway _dbsVar;
        internal IAdapterGateway _dbs { 
            get { return _dbsVar; }
            set {
                _dbsVar = value;
                if(value != null && value.GetType().GetInterfaces().Any(p=> p == typeof(IModularGateway))){
                    //if dbs is also a db module service, 
                    _dbms = (IModularGateway)value;
                }}
        } 

        IModularGateway _dbms;
        //This is nothing but a proxy, which also allows DBAdapter calls.
        public void Dispose() => SQLHandler.Dispose();
        //public void Dispose() { }
        public void Commit() => SQLHandler.Commit();
        public void Rollback() => SQLHandler.Rollback();
        public IDBTransaction Begin(bool ExcludeDBInConnectionString = false) {
            SQLHandler.Begin(ExcludeDBInConnectionString); //Do not return this object.
            return this; //Send this as the transaction.
        }
        public IModuleArgs CreateDBInput() {
            return CreateDBInput(new ModuleArgs());
        }

        public IModuleArgs CreateDBInput(IModuleArgs arg) {
            if (arg != null && arg is ModuleArgs argMP) {
                argMP.Adapter = this; //Main purpose is to send same adapter to the executors so that the transaction can be achieved.
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

        public Task<IFeedback> Execute(Enum cmd, IModuleArgs arg) {
            ValidateDBService();
            //Now, we need to attach the adapter to the argument.
            if (arg != null && arg is ModuleArgs argMP) {
                argMP.Adapter = this; //Main purpose is to send same adapter to the executors so that the transaction can be achieved.
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

// See https://aka.ms/new-console-template for more information
using Haley.Models;
using Haley.Utils;
//using Haley.Extensions;

var dbs = new DBService();

Console.WriteLine("Hello, World!");

//var dba = new DBAdapterDictionary();
//dba.Configure();

//var result = (await dba["dbsql"].ExecuteReader("select * from object limit 10", null)).Select(true).Convert(null);
//Console.WriteLine("OMG");
// ####### MS SQL

//var cstr = $@"server=srv-db07;database=;uid=CDEUser;pwd=; TrustServerCertificate=True;";
//var res = MssqlHandler.ExecuteReader(cstr, "SELECT name FROM master.dbo.sysdatabases", null).Result;
//var result = res.Select(true).Convert(null);


// ####### MYSQL
//var cstr = $@"server=localhost;port=3306;database=accounts__;uid=root;pwd=";
//var res = MysqlHandler.ExecuteReader(cstr, "Show databases", null).Result;
//var result = res.Select(true).Convert(null);
//Console.WriteLine("End");


// ####### MARIADB
//var cstr = $@"server=localhost;port=4306;database=d_test;uid=root;pwd=";
//var res = MysqlHandler.ExecuteReader(cstr, "Show databases", null).Result;
//var result = res.Select(true).Convert(null);
//Console.WriteLine("End");

// ####### POSTGRESQL
//var cstr = $@"Host=localhost;Port=5432;Username=postgres;Password=;Database=di_schema"; 
//var res = PgsqlHandler.ExecuteReader(cstr, "select nspname as name from pg_catalog.pg_namespace where nspname not like 'pg_%'", null).Result;
//var result = res.Select(true).Convert(null);
//Console.WriteLine("End");
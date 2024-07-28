using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Enums {
    public enum TargetDB {
        [Description("Empty")]
        unknown,
        [Description("Maria DB")]
        maria,
        [Description("MySQL")]
        mysql,
        [Description("Microsoft SQL")]
        mssql,
        [Description("PostgreSQL")]
        pgsql,
        [Description("SQLite")]
        sqlite
    }
}

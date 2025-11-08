using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml.Linq;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal class QRY_MARIA {
        public const string SCHEMA_EXISTS = $@"select 1 from information_schema.schemata where schema_name = {NAME};";
    }
}
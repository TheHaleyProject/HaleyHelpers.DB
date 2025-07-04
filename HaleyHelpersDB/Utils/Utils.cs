using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Haley.Utils
{
    public static class CreationUtils
    {

        //private static readonly Dictionary<string, ConstructorInfo> _constructorCache = new();
        //public static System.Runtime.CompilerServices.ITuple CreateCachedTuple(params object[] values) {
        //    string key = string.Join("|", values.Select(v => v.GetType().FullName));

        //    if (!_constructorCache.TryGetValue(key, out var ctor)) {
        //        var tupleType = Type.GetType($"System.Tuple`{values.Length}")
        //                         ?.MakeGenericType(values.Select(v => v.GetType()).ToArray());
        //        ctor = tupleType?.GetConstructor(values.Select(v => v.GetType()).ToArray());
        //        _constructorCache[key] = ctor;
        //    }

        //    return (ITuple)ctor?.Invoke(values);
        //}

    }
}

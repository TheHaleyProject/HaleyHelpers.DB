using Haley.Enums;

namespace Haley.Utils {
    internal static class InternalExtensions {
        internal static object ApplyFilter(this object input, ResultFilter filter) {
            if (filter == ResultFilter.None || input == null) return input; //Return result as is.
            //var inputObj = await input;
            if (input is List<Dictionary<string, object>> dicList && dicList.Count() > 0) {

                switch (filter) {
                    case ResultFilter.FullList:
                    return dicList;
                    case ResultFilter.FullListValues:
                    return dicList.SelectMany(p => p.Values.Select(q => q)).ToList();
                    case ResultFilter.FullListValueArray:
                    return dicList.Select(p => p.Values.ToList()).ToList();
                    case ResultFilter.FirstDictionary:
                    return dicList.First();
                    case ResultFilter.FirstDictionaryValue:
                    if (dicList.First()?.First() != null) return dicList.First().First().Value;
                    return dicList.First().First();
                }
            }
            return input;
        }
    }
}

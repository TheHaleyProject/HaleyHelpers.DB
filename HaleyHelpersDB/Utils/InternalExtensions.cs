using Haley.Enums;

namespace Haley.Utils {
    internal static class InternalExtensions {
        internal static object ApplyFilter(this object input, ResultFilter filter) {
            if (filter == ResultFilter.None || input == null) return input; //Return result as is.
            //var inputObj = await input;
            //If we limit the results by count(), we might end up creating wrong results.. For example, a method might be execpting First Dictionary based on filter, but we will return a list of dictionaries instead, which is wrong.
            //if (input is List<Dictionary<string, object>> dicList && dicList.Count() > 0) {
            if (input is List<Dictionary<string, object>> dicList) {
                switch (filter) {
                    case ResultFilter.FullList:
                    return dicList;
                    case ResultFilter.FullListValues:
                    return dicList.SelectMany(p => p.Values.Select(q => q)).ToList(); //may be empty
                    case ResultFilter.FullListValueArray:
                    return dicList.Select(p => p.Values.ToList()).ToList();
                    case ResultFilter.FirstDictionary:
                    return dicList.FirstOrDefault(); //may be null
                    case ResultFilter.FirstDictionaryValue:
                        if (dicList.FirstOrDefault() == null) return null;
                        if (dicList.FirstOrDefault()?.FirstOrDefault() != null) return dicList.FirstOrDefault().FirstOrDefault().Value;
                        return dicList.FirstOrDefault()?.FirstOrDefault();
                }
            }
            return input;
        }
    }
}

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
                    case ResultFilter.FlattenedValuesList:
                    return dicList.SelectMany(p => p.Values.Select(q => q)).ToList(); //may be empty
                    case ResultFilter.NestedValuesList:
                    return dicList.Select(p => p.Values.ToList()).ToList();
                    case ResultFilter.FirstColumnValuesList:
                        // return dicList.Select(p=> p.Values.FirstOrDefault()).ToList(); //No need for select many as we are only trying to fetch one value from each dictionary
                        //What if all dictionaries are not properly ordered?
                            var firstKey = dicList.FirstOrDefault()?.Keys.FirstOrDefault();
                            if (firstKey == null) return new List<object>();
                            return dicList.Select(d => d.TryGetValue(firstKey, out var v) ? v : null).Where(p=> p != null).ToList();
                    case ResultFilter.FirstDictionary:
                    return dicList.FirstOrDefault(); //may be null
                    case ResultFilter.FirstDictionaryValue:
                        var firstKvp = dicList.FirstOrDefault()?.FirstOrDefault();
                        if (firstKvp == null) return null;
                        return firstKvp.Value;
                }
            }
            return input;
        }
    }
}

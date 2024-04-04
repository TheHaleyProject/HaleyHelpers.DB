using Haley.Enums;

namespace Haley.Abstractions {
    public interface IDBServiceUtil  {
        public Task<object> GetFirst(object input, ResultFilter filter = ResultFilter.FirstDictionaryValue);
    }
}

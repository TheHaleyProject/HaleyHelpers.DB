using Haley.Abstractions;
using Haley.Services;

namespace Haley.Models {
    public class DbInstanceMaker {
        public string AdapterKey { get; set; } = string.Empty;
        public string FallbackDbName { get; protected set; } = string.Empty;
        public string SqlContent { get; protected set; } = string.Empty;
        public string ReplaceDbName { get; protected set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public IServiceProvider? ServiceProvider { get; set; } = null;
    }
}

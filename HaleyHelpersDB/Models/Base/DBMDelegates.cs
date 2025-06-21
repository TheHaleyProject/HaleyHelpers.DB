using Haley.Abstractions;

namespace Haley.Models
{
    public delegate Task<IFeedback> DBMExecuteDelegate(IModuleArgs parameter);
}

using System.Collections.Generic;

namespace AlwaysUp.Monitor
{
    interface IMonitorCondition
    {
        Dictionary<string, IDictionary<string, bool>> CreateEventFilters();
        Dictionary<string, IDictionary<string, bool>> CreateContainerFilters();
    }
}
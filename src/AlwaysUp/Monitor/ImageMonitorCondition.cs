using System.Collections.Generic;

namespace AlwaysUp.Monitor
{
    public class ImageMonitorCondition : IMonitorCondition
    {
        public string ImageName { get; private set; }
        public ImageMonitorCondition(string imageName)
        {
            ImageName = imageName;
        }

        public Dictionary<string, IDictionary<string, bool>> CreateEventFilters()
        {
            var filters = new Dictionary<string, IDictionary<string, bool>>(){
                { "type", new Dictionary<string, bool> { { "container", true } } },
                { "event", new Dictionary<string, bool> { { "create", true }, { "destroy", true } } },
                { "image", new Dictionary<string, bool> { { ImageName, true } } }
            };
            return filters;
        }

        public Dictionary<string, IDictionary<string, bool>> CreateContainerFilters()
        {
            var filters = new Dictionary<string, IDictionary<string, bool>>(){
                { "ancestor", new Dictionary<string, bool> { { ImageName, true } } }
            };
            return filters;
        }
    }
}
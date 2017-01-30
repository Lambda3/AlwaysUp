using System.Threading.Tasks;
using Docker.DotNet;

namespace AlwaysUp.Monitor
{
    public static class ContainerExtensions
    {
        public static async Task<bool> ContainerExistsAsync(this IContainerOperations containers, string containerId)
        {
            try
            {
                await containers.InspectContainerAsync(containerId);
                return true;
            }
            catch (DockerContainerNotFoundException)
            {
                return false;
            }
        }
    }
}
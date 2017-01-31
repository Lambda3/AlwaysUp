using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AlwaysUp.Monitor
{
    public class ClosedHttpMonitorTest : IMonitorTest
    {
        private readonly int port;
        private readonly ILogger logger;
        private readonly DockerClient client;
        private readonly string host;
        private readonly string path;

        public ClosedHttpMonitorTest(DockerClient client, ILoggerFactory loggerFactory, string host = null, int port = 80, string path = "")
        {
            this.client = client;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.host = host;
            this.port = port;
            this.path = path;
        }

        public async Task<bool> VerifyServiceIsUpAsync(string containerId, CancellationToken cancellationToken)
        {
            var exists = await client.Containers.ContainerExistsAsync(containerId);
            if (!exists)
            {
                logger.LogWarning($"Container '{containerId}' does not exist.");
                return false;
            }
            var inspection = await client.Containers.InspectContainerAsync(containerId);
            if (!inspection.NetworkSettings.Networks.Any())
            {
                logger.LogWarning($"Container '{containerId}' has no networks. ClosedHttpMonitor will default to false.");
                return false;
            }
            var network = inspection.NetworkSettings.Networks.First();
            var commandText = $"wget --quiet -O - {(host == null ? "" : $"--header 'HOST={host}:{port}' ")}http://{network.Value.IPAddress}:{port}/{path}";
            logger.LogDebug($"Running command '{commandText}'.");
            var createContainerParameters = new CreateContainerParameters
            {
                Image = "cirros",
                Entrypoint = new List<string>(commandText.Split(' ')),
                Tty = true,
                OpenStdin = true,
                HostConfig = new HostConfig(),
                NetworkingConfig = new NetworkingConfig
                {
                    EndpointsConfig = new Dictionary<string, EndpointSettings>{
                        {network.Key, new EndpointSettings
                        {
                        }}
                    }
                }
            };
            var success = false;
            var createContainerResponse = await client.Containers.CreateContainerAsync(createContainerParameters);
            var monitorContainerId = createContainerResponse.ID;
            logger.LogDebug($"Created new container '{monitorContainerId}' to verify container '{containerId}'.");
            if (createContainerResponse.Warnings != null)
                foreach (var warning in createContainerResponse.Warnings)
                    logger.LogWarning(warning);
            var started = await client.Containers.StartContainerAsync(monitorContainerId, new ContainerStartParameters());
            if (started)
            {
                logger.LogDebug($"Started new container '{monitorContainerId}'.");
                var timeout = Task.Delay(90000);
                var waitContainer = client.Containers.WaitContainerAsync(monitorContainerId, cancellationToken);
                logger.LogDebug($"Waiting for container '{monitorContainerId}'.");
                await Task.WhenAny(waitContainer, timeout);
                if (waitContainer.IsCompleted)
                {
                    var containerWaitResponse = waitContainer.Result;
                    logger.LogDebug($"Got status code {containerWaitResponse.StatusCode}.");
                    success = containerWaitResponse.StatusCode == 0;
                }
                else
                {
                    logger.LogDebug($"Did not finish the wait on the container '{monitorContainerId}'. Now killing it.");
                    await client.Containers.KillContainerAsync(monitorContainerId, new ContainerKillParameters());
                    logger.LogDebug($"Killed container '{monitorContainerId}'.");
                }
                logger.LogDebug($"Removing container '{monitorContainerId}'.");
                await client.Containers.RemoveContainerAsync(monitorContainerId, new ContainerRemoveParameters { Force = true });
            }
            else
            {
                logger.LogError($"Not able to start new container '{containerId}'.");
            }
            logger.LogDebug($"Finished test on Closed Http with success: {success}.");
            return success;
        }
    }
}

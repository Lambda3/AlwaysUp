using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AlwaysUp.Monitor
{
    public class MariaDbMonitorTest : IMonitorTest
    {
        private readonly int port;
        private readonly ILogger logger;
        private readonly DockerClient client;

        public MariaDbMonitorTest(DockerClient client, ILoggerFactory loggerFactory, int port = 3306)
        {
            this.client = client;
            this.port = port;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
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
                logger.LogWarning($"Container '{containerId}' has no networks. MariaDbMonitor will default to false.");
                return false;
            }
            var network = inspection.NetworkSettings.Networks.First();
            var createContainerParameters = new CreateContainerParameters
            {
                Image = "mariadb",
                Entrypoint = new List<string> { "mysqladmin", "-h", network.Value.IPAddress, "version" },
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
                    success = containerWaitResponse.StatusCode == 0;
                }
                else
                {
                    logger.LogDebug($"Did not finish the wait on the container '{monitorContainerId}'. Now killing it.");
                    await client.Containers.KillContainerAsync(monitorContainerId, new ContainerKillParameters());
                    logger.LogDebug($"Killed container '{monitorContainerId}'.");
                }
                logger.LogDebug($"Getting logs for container '{monitorContainerId}'.");
                // log fails with error
                // using (var logsStream = await client.Containers.GetContainerLogsAsync(monitorContainerId, new ContainerLogsParameters(), cancellationToken))
                // using (var logReader = new StreamReader(logsStream))
                // {
                //     var logs = logReader.ReadToEnd();
                //     logger.LogDebug($"Logs for container '{monitorContainerId}':\n{logs}");
                // }
                logger.LogDebug($"Removing container '{monitorContainerId}'.");
                await client.Containers.RemoveContainerAsync(monitorContainerId, new ContainerRemoveParameters { Force = true });
            }
            else
            {
                logger.LogError($"Not able to start new container '{containerId}'.");
            }
            logger.LogDebug($"Finished test on MariaDb with success: {success}.");
            return success;
        }
    }
}
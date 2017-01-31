using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AlwaysUp.Monitor
{
    class DestroyAndRecreateFixer : IServiceFixer
    {
        private readonly DockerClient client;
        private readonly ILogger logger;

        public DestroyAndRecreateFixer(DockerClient client, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.client = client;
        }

        public async Task<bool> FixAsync(string containerId, CancellationToken cancellationToken)
        {
            if (!await client.Containers.ContainerExistsAsync(containerId))
            {
                logger.LogWarning($"Container '{containerId}' does not exist.");
                return false;
            }
            var inspection = await client.Containers.InspectContainerAsync(containerId);
            var containerName = inspection.Name;
            if (containerName.StartsWith("/")) containerName = containerName.Substring(1);
            if (inspection.State.Running)
            {
                logger.LogDebug($"Stopping container {containerName} ({containerId}).");
                await client.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }, cancellationToken);
                logger.LogInformation($"Container {containerName} ({containerId}) stopped.");
            }
            if (await client.Containers.ContainerExistsAsync(containerId))
            {
                logger.LogDebug($"Removing container {containerName} ({containerId}).");
                await client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
                logger.LogInformation($"Container {containerName} ({containerId}) removed.");
            }
            logger.LogDebug($"Create new container using same settings from {containerName} ({containerId}).");
            var createContainerResponse = await client.Containers.CreateContainerAsync(CloneConfig(inspection));
            var newContainerId = createContainerResponse.ID;
            logger.LogInformation($"Created new container '{newContainerId}' using same settings from {containerName} ({containerId}).");
            if (createContainerResponse.Warnings != null)
                foreach (var warning in createContainerResponse.Warnings)
                    logger.LogWarning(warning);
            logger.LogDebug($"Starting new container using same settings from {containerName} ({containerId}).");
            var started = await client.Containers.StartContainerAsync(newContainerId, new ContainerStartParameters());
            if (started)
                logger.LogInformation($"Started new container '{containerName}' ({newContainerId}).");
            else
                logger.LogError($"Not able to start new container '{containerName}' ({newContainerId}).");
            return started;
        }

        private static CreateContainerParameters CloneConfig(ContainerInspectResponse inspection)
        {
            var createContainerParameters = new CreateContainerParameters
            {
                ArgsEscaped = inspection.Config.ArgsEscaped,
                AttachStderr = inspection.Config.AttachStderr,
                AttachStdin = inspection.Config.AttachStdin,
                AttachStdout = inspection.Config.AttachStdout,
                Cmd = inspection.Config.Cmd,
                Domainname = inspection.Config.Domainname,
                Entrypoint = inspection.Config.Entrypoint,
                Env = inspection.Config.Env,
                ExposedPorts = inspection.Config.ExposedPorts,
                Healthcheck = inspection.Config.Healthcheck,
                HostConfig = inspection.HostConfig,
                Hostname = inspection.Config.Hostname,
                Image = inspection.Config.Image,
                Labels = inspection.Config.Labels,
                MacAddress = inspection.Config.MacAddress,
                Name = inspection.Name,
                NetworkDisabled = inspection.Config.NetworkDisabled,
                NetworkingConfig = new NetworkingConfig { EndpointsConfig = inspection.NetworkSettings.Networks },
                OnBuild = inspection.Config.OnBuild,
                OpenStdin = inspection.Config.OpenStdin,
                Shell = inspection.Config.Shell,
                StdinOnce = inspection.Config.StdinOnce,
                StopSignal = inspection.Config.StopSignal,
                StopTimeout = inspection.Config.StopTimeout,
                Tty = inspection.Config.Tty,
                User = inspection.Config.User,
                Volumes = inspection.Config.Volumes,
                WorkingDir = inspection.Config.WorkingDir
            };
            return createContainerParameters;
        }
    }
}
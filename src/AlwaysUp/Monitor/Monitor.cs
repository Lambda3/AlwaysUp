using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AlwaysUp.Monitor
{
    class Monitor
    {
        private readonly DockerClient client;
        private readonly ILogger logger;
        private readonly IMonitorTest test;
        private const int intervalInMilliseconds = 5000;
        private Dictionary<string, IDictionary<string, bool>> eventFilters = new Dictionary<string, IDictionary<string, bool>>();
        private Dictionary<string, IDictionary<string, bool>> containerFilters = new Dictionary<string, IDictionary<string, bool>>();
        private readonly IServiceFixer fixer;
        private IImmutableList<string> containerIds = ImmutableList<string>.Empty;
        public Monitor(DockerClient client, ILoggerFactory loggerFactory, IMonitorCondition condition, IMonitorTest test, IServiceFixer fixer)
        {
            this.fixer = fixer;
            this.client = client;
            this.test = test;
            eventFilters = condition.CreateEventFilters();
            containerFilters = condition.CreateContainerFilters();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public async void StartAsync(CancellationToken cancellationToken)
        {
            ListenToDockerEventsAsync(cancellationToken);
            await GatherRunningContainersAsync(cancellationToken);
            StartMonitoringAsync(cancellationToken);
        }

        private async void StartMonitoringAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var containerId in containerIds)
                {
                    try
                    {
                        var isUp = await test.VerifyServiceIsUpAsync(containerId, cancellationToken);
                        if (!isUp)
                        {
                            await fixer.FixAsync(containerId, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Error while monitoring container '{containerId}'. Details:\n{ex.ToString()}");
                    }
                }
                await Task.Delay(intervalInMilliseconds);
            }
        }

        private async void ListenToDockerEventsAsync(CancellationToken cancellationToken)
        {
            var containerEventParameters = new ContainerEventsParameters { Filters = eventFilters };
            using (var stream = await client.Miscellaneous.MonitorEventsAsync(containerEventParameters, cancellationToken))
            {
                using (var reader = new System.IO.StreamReader(stream))
                {
                    while (!cancellationToken.IsCancellationRequested && !reader.EndOfStream)
                    {
                        try
                        {
                            var line = await reader.ReadLineAsync();
                            logger.LogDebug(line);
                            dynamic containerEvent = JsonConvert.DeserializeObject(line);
                            string containerId = containerEvent.id;
                            string action = containerEvent.Action;
                            logger.LogDebug($"Container id is '{containerId}' and action is '{action}'.");
                            if (action == "create")
                            {
                                containerIds = containerIds.Add(containerId);
                            }
                            else if (action == "destroy")
                            {
                                if (containerIds.IndexOf(containerId) > -1)
                                    containerIds = containerIds.Remove(containerId);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            logger.LogError($"Caught error reading Docker events.\n{ex.ToString()}");
                        }
                    }
                }
            }
        }

        private async Task GatherRunningContainersAsync(CancellationToken cancellationToken)
        {
            var containersListParameters = new ContainersListParameters { Filters = containerFilters };
            var containers = await client.Containers.ListContainersAsync(containersListParameters);
            foreach (var container in containers)
            {
                logger.LogDebug($"Found '{container.ID}'");
                containerIds = containerIds.Add(container.ID); //todo linq?
            }
        }
    }
}
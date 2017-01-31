using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Microsoft.Extensions.Logging;

namespace AlwaysUp.Monitor
{
    class MonitorAgent
    {
        private readonly DockerClient client;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly ILogger logger;
        private readonly List<Monitor> monitors = new List<Monitor>();
        private readonly ILoggerFactory loggerFactory;

        private MonitorAgent(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public MonitorAgent(ILoggerFactory loggerFactory, string host = "localhost", int port = 2375) : this(loggerFactory)
        {
            client = new DockerClientConfiguration(new Uri($"http://{host}:{port}")).CreateClient();
        }

        public async void StartAsync()
        {
            await LoadMonitoringConfigAsync();
        }

        private Task LoadMonitoringConfigAsync()
        {
            monitors.Add(new Monitor(client, loggerFactory,
                new ImageMonitorCondition("webfailevery4"),
                new ClosedHttpMonitorTest(client, loggerFactory, port: 9000),
                new DestroyAndRecreateFixer(client, loggerFactory)));
            monitors.Add(new Monitor(client, loggerFactory,
                new ImageMonitorCondition("foo"),
                new HttpMonitorTest(loggerFactory, port: 8000),
                new DestroyAndRecreateFixer(client, loggerFactory)));
            monitors.Add(new Monitor(client, loggerFactory,
                new ImageMonitorCondition("mariadb"),
                new MariaDbMonitorTest(client, loggerFactory),
                new DestroyAndRecreateFixer(client, loggerFactory)));
            foreach (var monitor in monitors)
                monitor.StartAsync(cancellation.Token);
            return Task.CompletedTask;
        }

        public void Stop()
        {
            logger.LogInformation("Stopping.");
            cancellation.Cancel();
        }
    }
}
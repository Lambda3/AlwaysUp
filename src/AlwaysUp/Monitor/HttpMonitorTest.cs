using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AlwaysUp.Monitor
{
    public class HttpMonitorTest : IMonitorTest
    {
        private static HttpClient httpClient = new HttpClient();
        private readonly string host;
        private readonly int port;
        private readonly string path;
        private readonly ILogger logger;

        public HttpMonitorTest(ILoggerFactory loggerFactory, string host = "localhost", int port = 80, string path = "")
        {
            this.path = path;
            this.port = port;
            this.host = host;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public async Task<bool> VerifyServiceIsUpAsync(string containerId, CancellationToken cancellationToken)
        {
            var url = $"http://{host}:{port}/{path}";
            logger.LogDebug($"Testing '{url}'...");
            try
            {
                var result = await httpClient.GetAsync(url, cancellationToken);
                logger.LogDebug($"Got {result.StatusCode} for '{url}'.");
                return result.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                logger.LogDebug($"Got an http error when testing for '{url}'. {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                logger.LogDebug($"Got task cancelled exception.");
            }
            catch (Exception ex)
            {
                logger.LogDebug($"Got an error when testing for '{url}'. Details:\n{ex}");
            }
            return false;
        }
    }
}
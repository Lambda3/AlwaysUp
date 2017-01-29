using System.Threading;
using System.Threading.Tasks;

namespace AlwaysUp.Monitor
{
    interface IMonitorTest
    {
        Task<bool> VerifyServiceIsUpAsync(string containerId, CancellationToken cancellationToken);
    }
}
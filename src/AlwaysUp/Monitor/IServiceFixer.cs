using System.Threading;
using System.Threading.Tasks;

namespace AlwaysUp.Monitor
{
    public interface IServiceFixer
    {
        Task FixAsync(string containerId, CancellationToken cancellationToken);
    }
}
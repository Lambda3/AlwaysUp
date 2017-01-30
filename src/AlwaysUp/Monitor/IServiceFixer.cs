using System.Threading;
using System.Threading.Tasks;

namespace AlwaysUp.Monitor
{
    public interface IServiceFixer
    {
        Task<bool> FixAsync(string containerId, CancellationToken cancellationToken);
    }
}
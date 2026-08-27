using Kofoten.NativeCli.Internal;
using System.Threading;
using System.Threading.Tasks;

namespace Kofoten.NativeCli;

public interface IAsyncCliCommand : ICliParsable
{
    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The exit code of the command.</returns>
    Task<int> ExecuteAsync(CancellationToken cancellationToken);
}

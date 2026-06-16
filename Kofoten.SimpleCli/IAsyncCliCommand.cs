using System.Threading;
using System.Threading.Tasks;

namespace Kofoten.SimpleCli;

public interface IAsyncCliCommand : ICliParsable
{
    Task<int> ExecuteAsync(CancellationToken cancellationToken);
}

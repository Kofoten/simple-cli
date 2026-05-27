using System.Threading.Tasks;

namespace Kofoten.SimpleCli;

public interface IAsyncCliCommand
{
    Task<int> ExecuteAsync();
}

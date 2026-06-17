namespace Kofoten.SimpleCli;

public interface ICliCommand : ICliParsable
{
    /// <summary>
    /// Executes the command synchronously.
    /// </summary>
    /// <returns>The exit code of the command.</returns>
    int Execute();
}

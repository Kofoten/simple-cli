namespace Kofoten.SimpleCli;

public interface ICliCommand : ICliParsable
{
    int Execute();
}

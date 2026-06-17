namespace Kofoten.SimpleCli;

public interface ICliFactory
{
    CliCommand GetCommand(string[] args);
    string GetUsage();
}

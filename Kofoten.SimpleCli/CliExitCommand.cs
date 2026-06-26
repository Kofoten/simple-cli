namespace Kofoten.SimpleCli;

public class CliExitCommand(int exitCode) : ICliCommand
{
    public int Execute()
    {
        return exitCode;
    }
}

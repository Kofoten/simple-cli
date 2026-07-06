using Kofoten.SimpleCli.Test.Data;

namespace Kofoten.SimpleCli.Test;

internal abstract class BaseCommand : ICliCommand
{
    [CliOption("weather", Short = 'w', Description = "Whats the weather")]
    public Weather Weather { get; init; } = Weather.Sunny;

    public abstract int Execute();
}

using Kofoten.SimpleCli;
using Kofoten.SimpleCli.DependencyInjection;
using Kofoten.SimpleCli.Test;
using Microsoft.Extensions.DependencyInjection;

try
{
    // Let's test the happy path with the greedy collection and both boolean flags
    string[] simulatedArgs = ["10", "20", "-a", "5", "15", "--verbose", "--table"];

    Console.WriteLine($"Simulating single command app args: {string.Join(" ", simulatedArgs)}\n");

    var command = AdditionCommandParser.Parse(simulatedArgs, new());

    // Execute your handcrafted logic!
    command.Execute();
}
catch (AggregateException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Command failed with the following errors:");
    foreach (var inner in ex.InnerExceptions)
    {
        Console.WriteLine($"- {inner.Message}");
    }
    Console.ResetColor();
}

try
{
    // Let's test the happy path with the greedy collection and both boolean flags
    string[] simulatedArgs = ["math", "add", "10", "20", "-a", "5", "15", "--verbose", "--table", "-w", "rainy,sunny", "snowy", "--weather", "cloudy", "--indexed-cheese", "7=Herrgård|Sweden", "--frozen-cheese", "Brie|France", "-l", "55", "-s", "Kofoten"];

    Console.WriteLine($"Simulating multi command app args: {string.Join(" ", simulatedArgs)}\n");

    var router = new CliCommandRouter(ExceptionHandler);
    router.Map("math", sr =>
    {
        sr.MapAdditionCommand("add", new());
    });

    var command = router.GetCommand(simulatedArgs);

    // Execute your handcrafted logic!
    command.Execute();
}
catch (AggregateException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Command failed with the following errors:");
    foreach (var inner in ex.InnerExceptions)
    {
        Console.WriteLine($"- {inner.Message}");
    }
    Console.ResetColor();
}

try
{
    // Let's test the happy path with the greedy collection and both boolean flags
    string[] simulatedArgs = ["add", "10", "20", "-a", "5", "15", "--verbose", "--table", "--cheese", "Gouda|Netherlands"];

    Console.WriteLine($"Simulating multi command app args: {string.Join(" ", simulatedArgs)}\n");

    var services = new ServiceCollection();
    services.AddSingleton(new object());
    services.AddCliCommands(simulatedArgs, ExceptionHandler, router =>
    {
        router.MapAdditionCommand("add");
    });

    var provider = services.BuildServiceProvider();
    var command = provider.GetRequiredService<CliCommand>();

    // Execute your handcrafted logic!
    command.Execute();
}
catch (AggregateException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Command failed with the following errors:");
    foreach (var inner in ex.InnerExceptions)
    {
        Console.WriteLine($"- {inner.Message}");
    }
    Console.ResetColor();
}

static int ExceptionHandler(Exception exception, IServiceProvider? _)
{
    if (exception is CliParseException parseException)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Command failed with the following errors:");
        foreach (var error in parseException.Errors)
        {
            Console.WriteLine($"- {error}");
        }
        Console.ResetColor();
        Console.WriteLine(parseException.HelpText);

        return 1;
    }

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("An unknown error occurred:");
    Console.WriteLine(exception.Message);
    Console.ResetColor();
    Console.WriteLine(exception.StackTrace);

    return 42;
}

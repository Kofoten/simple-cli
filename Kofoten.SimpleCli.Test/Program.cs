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
    string[] simulatedArgs = ["math", "add", "10", "20", "-a", "5", "15", "--verbose", "--table", "-w", "rainy,sunny", "snowy", "--weather", "cloudy"];

    Console.WriteLine($"Simulating multi command app args: {string.Join(" ", simulatedArgs)}\n");

    var router = new CliCommandRouter(ErrorHandler);
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
    services.AddCliCommands(simulatedArgs, ErrorHandler, router =>
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

static int ErrorHandler(IEnumerable<string> errors, string helpText)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Command failed with the following errors:");
    foreach (var error in errors)
    {
        Console.WriteLine($"- {error}");
    }
    Console.ResetColor();
    Console.WriteLine(helpText);
    return 1;
}
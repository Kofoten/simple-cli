using Kofoten.SimpleCli;
using Kofoten.SimpleCli.DependencyInjection;
using Kofoten.SimpleCli.Test;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

string[] simulatedArgs = ["10", "20", "-a", "5", "15", "--verbose", "--table", "-w", "rainy,sunny", "snowy", "--weather", "cloudy", "--indexed-cheese", "7=Herrgård|Sweden", "--frozen-cheese", "Brie|France", "-l", "55", "-s", "Kofoten"];

try
{
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
    string[] route = ["math", "add"];
    var simulatedArgsWithRouting = route.Concat(simulatedArgs).ToArray();

    Console.WriteLine($"Simulating multi command app args: {string.Join(" ", simulatedArgsWithRouting)}\n");

    var builder = CliCommandBuilder.Configure(router =>
    {
        router.Map("math", sr =>
        {
            sr.MapAdditionCommand("add", new());
        });
    }, ExceptionHandler);

    var command = builder.ToCommand(simulatedArgsWithRouting);

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
    string[] route = ["add"];
    var simulatedArgsWithRouting = route.Concat(simulatedArgs).ToArray();

    Console.WriteLine($"Simulating dependency injection app: {string.Join(" ", simulatedArgsWithRouting)}\n");

    var command = new ServiceCollection()
        .AddSingleton(new object())
        .AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        })
        .AddCliCommands(simulatedArgsWithRouting, router =>
        {
            router.MapAdditionCommand("add");
        }, ExceptionHandler)
        .BuildServiceProvider()
        .GetRequiredService<CliCommand>();

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

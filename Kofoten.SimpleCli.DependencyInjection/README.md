# Dependendy injection shim for Simple Cli

This is a shim providing dependency injection capabilities for [Kofoten.SimpleCli](https://github.com/Kofoten/simple-cli).

## Usage

Program.cs

```c#
using Kofoten.SimpleCli;
using Kofoten.SimpleCli.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

return new ServiceCollection()
    .AddSingleton(new object())
    .AddLogging(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Information);
    })
    .AddCliCommands(args, router =>
    {
        router.MapAdditionCommand("add");
    }, ExceptionHandler)
    .BuildServiceProvider()
    .GetRequiredService<CliCommand>()
    .Execute();

static int ExceptionHandler(Exception exception, IServiceProvider? sp)
{
    var logger = sp?.GetService<ILogger<Program>>();

    if (exception is CliParseException parseException)
    {
        if (logger is not null)
        {
            logger.FailedToParseArguments(parseException.Message);
        }
        else
        {
            Console.WriteLine(parseException.Message);
        }

        return 1;
    }

    if (logger is not null)
    {
        logger.UnhandledException(exception);
    }
    else
    {
        Console.WriteLine(exception.ToString());
    }

    return 42;
}
```
# Dependendy injection shim for Native Cli

This is a shim providing dependency injection capabilities for [Kofoten.NativeCli](https://github.com/Kofoten/native-cli).

## Usage

Program.cs

```c#
using Kofoten.NativeCli;
using Kofoten.NativeCli.DependencyInjection;
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
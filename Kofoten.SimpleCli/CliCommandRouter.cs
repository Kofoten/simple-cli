using System;

namespace Kofoten.SimpleCli;

public sealed class CliCommandRouter(string commandDescription, Func<Exception, IServiceProvider?, int> exceptionHandler) : CliCommandRouterBase<CliCommandRouter, Func<CliParseResult>>(commandDescription, exceptionHandler)
{
    public CliCommandRouter(Func<Exception, IServiceProvider?, int> exceptionHandler)
        : this(string.Empty, exceptionHandler)
    {
    }

    /// <summary>
    /// Resolves a command based on the provided arguments. The first argument is treated as the
    /// verb, and the remaining arguments are passed to the corresponding command factory.
    /// </summary>
    /// <param name="args">The arguments to resolve the command from.</param>
    /// <returns>The resolved command.</returns>
    public CliCommand GetCommand(string[] args)
        => CliCommand.CreateFromFactoryFunctionResult(
            GetFactoryFunction(args),
            (factoryFunction) => factoryFunction.Invoke(),
            exceptionHandler,
            null);

    protected override CliCommandRouter CreateSubRouter(string description) => new(description, exceptionHandler);
}

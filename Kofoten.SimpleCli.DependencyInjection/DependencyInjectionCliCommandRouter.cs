using System;

namespace Kofoten.SimpleCli.DependencyInjection;

public sealed class DependencyInjectionCliCommandRouter(string commandDescription, Func<Exception, IServiceProvider?, int> exceptionHandler)
    : CliCommandRouterBase<DependencyInjectionCliCommandRouter, Func<IServiceProvider, CliParseResult>>(commandDescription, exceptionHandler)
{
    public DependencyInjectionCliCommandRouter(Func<Exception, IServiceProvider?, int> exceptionHandler)
        : this(string.Empty, exceptionHandler)
    {
    }

    /// <summary>
    /// Resolves a command based on the provided arguments. The first argument is treated as the
    /// verb, and the remaining arguments are passed to the corresponding command factory.
    /// </summary>
    /// <param name="args">The arguments to resolve the command from.</param>
    /// <param name="serviceProvider">The service provider to resolve dependencies from.</param>
    /// <returns>The resolved command.</returns>
    public CliCommand GetCommand(string[] args, IServiceProvider serviceProvider)
        => CliCommand.CreateFromFactoryFunctionResult(
            GetFactoryFunction(args),
            (factoryFunction) => factoryFunction.Invoke(serviceProvider),
            exceptionHandler,
            serviceProvider);

    protected override DependencyInjectionCliCommandRouter CreateSubRouter(string description) => new(description, exceptionHandler);
}

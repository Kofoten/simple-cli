using System;
using System.Collections.Generic;

namespace Kofoten.SimpleCli.DependencyInjection;

public sealed class DependencyInjectionCliCommandRouter(string commandDescription, Func<IEnumerable<string>, string, int> errorHandler)
    : CliCommandRouterBase<DependencyInjectionCliCommandRouter, Func<IServiceProvider, CliParseResult>>(commandDescription, errorHandler)
{
    public DependencyInjectionCliCommandRouter(Func<IEnumerable<string>, string, int> errorHandler)
        : this(string.Empty, errorHandler)
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
    {
        var factoryResult = GetFactoryFunction(args);
        switch (factoryResult)
        {
            case CliFactoryFunctionResult<Func<IServiceProvider, CliParseResult>>.Success success:
                var parseResult = success.FactoryFunction.Invoke(serviceProvider);
                return ResolveParseResult(parseResult, success.HelpText);
            case CliFactoryFunctionResult<Func<IServiceProvider, CliParseResult>>.Failure failure:
                var exitCode = errorHandler.Invoke(failure.Errors, failure.HelpText);
                return Exit(exitCode);
            case CliFactoryFunctionResult<Func<IServiceProvider, CliParseResult>>.Usage usage:
                Console.WriteLine(usage.HelpText);
                return Exit(0);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override DependencyInjectionCliCommandRouter CreateSubRouter(string description) => new(description, errorHandler);
}

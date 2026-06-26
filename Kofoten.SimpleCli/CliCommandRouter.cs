using System;
using System.Collections.Generic;

namespace Kofoten.SimpleCli;

public sealed class CliCommandRouter(string commandDescription, Func<IEnumerable<string>, string, int> errorHandler) : CliCommandRouterBase<CliCommandRouter, Func<CliParseResult>>(commandDescription, errorHandler)
{
    public CliCommandRouter(Func<IEnumerable<string>, string, int> errorHandler)
        : this(string.Empty, errorHandler)
    {
    }

    /// <summary>
    /// Resolves a command based on the provided arguments. The first argument is treated as the
    /// verb, and the remaining arguments are passed to the corresponding command factory.
    /// </summary>
    /// <param name="args">The arguments to resolve the command from.</param>
    /// <returns>The resolved command.</returns>
    public CliCommand GetCommand(string[] args)
    {
        var factoryResult = GetFactoryFunction(args);
        switch (factoryResult)
        {
            case CliFactoryFunctionResult<Func<CliParseResult>>.Success success:
                var parseResult = success.FactoryFunction.Invoke();
                return ResolveParseResult(parseResult, success.HelpText);
            case CliFactoryFunctionResult<Func<CliParseResult>>.Failure failure:
                var exitCode = errorHandler.Invoke(failure.Errors, failure.HelpText);
                return new CliCommand(new CliExitCommand(exitCode));
            case CliFactoryFunctionResult<Func<CliParseResult>>.Usage usage:
                Console.WriteLine(usage.HelpText);
                return new CliCommand(new CliExitCommand(0));
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override CliCommandRouter CreateSubRouter(string description) => new(description, errorHandler);
}

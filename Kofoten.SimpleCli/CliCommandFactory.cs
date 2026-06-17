using System;

namespace Kofoten.SimpleCli;

internal class CliCommandFactory(string commandDescription, Func<ArraySegment<string>, ICliParsable> factoryFunction, Func<string, string> usageFunction) : ICliCommandFactory
{
    private readonly Func<ArraySegment<string>, ICliParsable> factoryFunction = factoryFunction;
    private readonly Func<string, string> usageFunction = usageFunction;

    public string CommandDescription { get; private set; } = commandDescription;

    public CliCommand GetCommand(ArraySegment<string> args)
    {
        var command = factoryFunction.Invoke(args);
        return new CliCommand(command);
    }

    public string GetUsage(string commandPath) => usageFunction.Invoke(commandPath);
}

public interface ICliCommandFactory
{
    string CommandDescription { get; }

    string GetUsage(string commandPath);
}
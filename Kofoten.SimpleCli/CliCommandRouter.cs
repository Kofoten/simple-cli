using System;
using System.Linq;

namespace Kofoten.SimpleCli;

public sealed class CliCommandRouter(string commandDescription) : CliCommandRouterBase<CliCommandRouter, Func<ArraySegment<string>, ICliParsable>>(commandDescription)
{
    public CliCommandRouter()
        : this(string.Empty)
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
        if (TryGetFactoryFunction(args, out var factoryFunction, out var error))
        {
            var command = factoryFunction(new ArraySegment<string>(args, 1, args.Length - 1));
            return new CliCommand(command);
        }
        ICliCommandFactory factory = this;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-h" || args[i] == "--help")
            {
                var path = string.Join(" ", args.Take(i));
                Console.WriteLine(factory.GetUsage(path));
                return new CliCommand(new CliDummyCommand());
            }

            if (factory is CliCommandFactory commandFactory)
            {
                return commandFactory.GetCommand(new ArraySegment<string>(args, i, args.Length - i));
            }

            if (factory is CliCommandRouter commandRouter
                &&
                commandRouter.factories.TryGetValue(args[i], out factory))
            {
                continue;
            }

            throw new ArgumentException($"Invalid verb: {args[i]}");
        }

        throw new ArgumentException($"Command '{string.Join(" ", args)}' requires at least one argument");
    }

    protected override CliCommandRouter CreateSubRouter(string description) => new(description);
}

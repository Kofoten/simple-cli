using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kofoten.SimpleCli;

public sealed class CliCommandRouter(string commandDescription) : ICliCommandFactory
{
    private readonly Dictionary<string, ICliCommandFactory> factories = [];

    public string CommandDescription { get; private set; } = commandDescription;

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

    /// <summary>
    /// Maps a verb to a command configuration. The provided configuration action allows you to define
    /// subcommands for the verb.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="configure">The configuration action to define subcommands.</param>
    public void Map(string verb, Action<CliCommandRouter> configure)
        => Map(verb, string.Empty, configure);

    /// <summary>
    /// Maps a verb to a command configuration. The provided configuration action allows you to define
    /// subcommands for the verb.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="description">The description of the group.</param>
    /// <param name="configure">The configuration action to define subcommands.</param>
    public void Map(string verb, string description, Action<CliCommandRouter> configure)
    {
        var router = new CliCommandRouter(description);
        configure(router);
        factories.Add(verb, router);
    }

    /// <summary>
    /// Maps a verb to a command factory. The factory is a function that takes the remaining arguments
    /// and returns an instance of <see cref="ICliParsable"/>.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="description">The description of the command.</param>
    /// <param name="factory">The factory function to create the command.</param>
    public void Map(string verb, string description, Func<ArraySegment<string>, ICliParsable> factoryFunction, Func<string, string> usageFunction)
    {
        factories.Add(verb, new CliCommandFactory(description, factoryFunction, usageFunction));
    }

    public string GetUsage(string commandPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CommandDescription);
        builder.AppendLine();
        builder.AppendLine("Usage:");
        builder.AppendLine($"  {commandPath} subcommands... <args> [options]");
        builder.AppendLine();
        builder.AppendLine("Subcommands:");

        var verbNameLength = factories.Max(x => x.Key.Length);
        foreach (var factory in factories.OrderBy(x => x.Key))
        {
            var verb = factory.Key.PadRight(verbNameLength, ' ');
            builder.AppendLine($"  {verb}  {factory.Value.CommandDescription}");
        }

        builder.AppendLine();
        builder.AppendLine("Options:");
        builder.AppendLine("  -h, --help  Displays this message.");

        return builder.ToString();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kofoten.SimpleCli;

public abstract class CliCommandRouterBase<TRouter, TFactoryFunction>(string commandDescription, Func<IEnumerable<string>, string, int> errorHandler) : ICliCommandFactory<TFactoryFunction>
    where TRouter : CliCommandRouterBase<TRouter, TFactoryFunction>, ICliCommandFactory<TFactoryFunction>
{
    private readonly Dictionary<string, ICliCommandFactory<TFactoryFunction>> factories = [];

    protected readonly Func<IEnumerable<string>, string, int> errorHandler = errorHandler;

    public bool IsLeaf => false;
    public string CommandDescription { get; private set; } = commandDescription;

    public CliCommandRouterBase(Func<IEnumerable<string>, string, int> errorHandler)
        : this(string.Empty, errorHandler)
    {
    }

    protected abstract TRouter CreateSubRouter(string description);

    /// <summary>
    /// Maps a verb to a command configuration. The provided configuration action allows you to define
    /// subcommands for the verb.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="configure">The configuration action to define subcommands.</param>
    public void Map(string verb, Action<TRouter> configure)
        => Map(verb, string.Empty, configure);

    /// <summary>
    /// Maps a verb to a command configuration. The provided configuration action allows you to define
    /// subcommands for the verb.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="description">The description of the group.</param>
    /// <param name="configure">The configuration action to define subcommands.</param>
    public void Map(string verb, string description, Action<TRouter> configure)
    {
        var router = CreateSubRouter(description);
        configure(router);
        factories.Add(verb, router);
    }

    /// <summary>
    /// Maps a verb to a command factory. The factory is a function that takes the remaining arguments
    /// and returns an instance of <see cref="ICliParsable"/>.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="factory">The factory function to create the command.</param>
    public void Map(string verb, ICliCommandFactory<TFactoryFunction> factory)
    {
        factories.Add(verb, factory);
    }

    public CliFactoryFunctionResult<TFactoryFunction> GetFactoryFunction(string[] args)
    {
        ICliCommandFactory<TFactoryFunction> factory = this;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-h" || args[i] == "--help")
            {
                return new CliFactoryFunctionResult<TFactoryFunction>.Usage(factory.GetUsage(string.Join(" ", args.Take(i))));
            }

            if (factory.IsLeaf)
            {
                return new CliFactoryFunctionResult<TFactoryFunction>.Success(factory.GetFactoryFunction(new ArraySegment<string>(args, i, args.Length - i)));
            }

            if (factory is TRouter commandRouter
                &&
                commandRouter.factories.TryGetValue(args[i], out factory))
            {
                continue;
            }

            var path = string.Join(" ", args.Take(i));
            return new CliFactoryFunctionResult<TFactoryFunction>.Failure([$"Invalid verb: {args[i]}"], factory.GetUsage(path));
        }

        var commandPath = string.Join(" ", args);
        return new CliFactoryFunctionResult<TFactoryFunction>.Failure([$"Command '{commandPath}' requires at least one argument"], factory.GetUsage(commandPath));
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

    public TFactoryFunction GetFactoryFunction(ArraySegment<string> args)
    {
        throw new NotImplementedException();
    }

    protected CliCommand ResolveParseResult(CliParseResult parseResult, string helpText)
    {
        switch (parseResult)
        {
            case CliParseResult.Success parseSuccess:
                return new CliCommand(parseSuccess.Parsable);
            case CliParseResult.Failure parseFailure:
                var exitCode = errorHandler.Invoke(parseFailure.Errors, helpText);
                return new CliCommand(new CliExitCommand(exitCode));
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kofoten.NativeCli;

public sealed class CliCommandRouter<TFactoryFunction>(string commandDescription, Func<Exception, IServiceProvider?, int> exceptionHandler) : ICliCommandFactory<TFactoryFunction>
{
    private readonly Dictionary<string, ICliCommandFactory<TFactoryFunction>> factories = [];

    private readonly Func<Exception, IServiceProvider?, int> exceptionHandler = exceptionHandler;

    public bool IsLeaf => false;
    public string CommandDescription { get; private set; } = commandDescription;

    public CliCommandRouter(Func<Exception, IServiceProvider?, int> exceptionHandler)
        : this(string.Empty, exceptionHandler)
    {
    }

    /// <summary>
    /// Maps a verb to a command configuration. The provided configuration action allows you to define
    /// subcommands for the verb.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="configure">The configuration action to define subcommands.</param>
    public void Map(string verb, Action<CliCommandRouter<TFactoryFunction>> configure)
        => Map(verb, string.Empty, configure);

    /// <summary>
    /// Maps a verb to a command configuration. The provided configuration action allows you to define
    /// subcommands for the verb.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="description">The description of the group.</param>
    /// <param name="configure">The configuration action to define subcommands.</param>
    public void Map(string verb, string description, Action<CliCommandRouter<TFactoryFunction>> configure)
    {
        var subRouter = new CliCommandRouter<TFactoryFunction>(description, exceptionHandler);
        configure.Invoke(subRouter);
        factories.Add(verb, subRouter);
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

            if (factory is CliCommandRouter<TFactoryFunction> commandRouter
                &&
                commandRouter.factories.TryGetValue(args[i], out var nextFactory))
            {
                factory = nextFactory;
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
}

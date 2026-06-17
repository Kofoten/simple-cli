using System;
using System.Collections.Generic;

namespace Kofoten.SimpleCli.DependencyInjection;

public class DependencyInjectionCliCommandRouter
{
    private readonly Dictionary<string, Func<ArraySegment<string>, IServiceProvider, CliCommand>> factories = [];

    /// <summary>
    /// Resolves a command based on the provided arguments. The first argument is treated as the
    /// verb, and the remaining arguments are passed to the corresponding command factory.
    /// </summary>
    /// <param name="args">The arguments to resolve the command from.</param>
    /// <param name="serviceProvider">The service provider to resolve dependencies from.</param>
    /// <returns>The resolved command.</returns>
    public CliCommand GetCommand(string[] args, IServiceProvider serviceProvider) => ResolveCommand(new ArraySegment<string>(args), serviceProvider);

    /// <summary>
    /// Maps a verb to a command configuration. The provided configuration action allows you to define
    /// subcommands for the verb.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="configure">The configuration action to define subcommands.</param>
    public void Map(string verb, Action<DependencyInjectionCliCommandRouter> configure)
    {
        var router = new DependencyInjectionCliCommandRouter();
        configure(router);
        factories.Add(verb, router.ResolveCommand);
    }

    /// <summary>
    /// Maps a verb to a command factory. The factory is a function that takes the remaining arguments
    /// and returns an instance of <see cref="ICliParsable"/>.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="factory">The factory function to create the command.</param>
    public void Map(string verb, Func<ArraySegment<string>, IServiceProvider, ICliParsable> factory)
    {
        factories.Add(verb, (args, sp) => new CliCommand(factory(args, sp)));
    }

    private CliCommand ResolveCommand(ArraySegment<string> args, IServiceProvider serviceProvider)
    {
        if (args.Count == 0)
        {
            throw new ArgumentException($"Command '{{command}}' requires at least one argument");
        }

        var verb = args.Array[args.Offset];
        if (factories.TryGetValue(verb, out var factory))
        {
            var subsegment = new ArraySegment<string>(args.Array, args.Offset + 1, args.Count - 1);
            return factory(subsegment, serviceProvider);
        }

        throw new ArgumentException($"Invalid verb: {verb}");
    }
}

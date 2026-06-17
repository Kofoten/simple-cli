using System;
using System.Collections.Generic;

namespace Kofoten.SimpleCli;

public abstract class CliCommandRouterBase<TFactory>(TFactory? parent)
    where TFactory : CliCommandRouterBase<TFactory>, ICliFactory
{
    private readonly TFactory? parent = parent;
    private readonly Dictionary<string, TFactory> factories = [];

    /// <summary>
    /// Maps a verb to a command configuration. The provided configuration action allows you to define
    /// subcommands for the verb.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="configure">The configuration action to define subcommands.</param>
    public void Map(string verb, Action<TFactory> configure)
    {
        var router = new TFactory(this);
        configure(router);
        factories.Add(verb, router);
    }

    /// <summary>
    /// Maps a verb to a command factory. The factory is a function that takes the remaining arguments
    /// and returns an instance of <see cref="ICliParsable"/>.
    /// </summary>
    /// <param name="verb">The verb to map.</param>
    /// <param name="factory">The factory function to create the command.</param>
    public void Map(string verb, TFactory factory)
    {
        factories.Add(verb, factory);
    }
}

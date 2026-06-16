using System;
using System.Collections.Generic;

namespace Kofoten.SimpleCli;

public sealed class CliCommandRouter
{
    private readonly Dictionary<string, Func<ArraySegment<string>, CliCommand>> factories = [];

    public CliCommand GetCommand(string[] args) => ResolveCommand(new ArraySegment<string>(args));

    public void Map(string verb, Action<CliCommandRouter> configure)
    {
        var router = new CliCommandRouter();
        configure(router);
        factories.Add(verb, router.ResolveCommand);
    }

    public void Map(string verb, Func<ArraySegment<string>, ICliParsable> factory)
    {
        factories.Add(verb, (args) => new CliCommand(factory(args)));
    }

    private CliCommand ResolveCommand(ArraySegment<string> args)
    {
        if (args.Count == 0)
        {
            throw new ArgumentException($"Command '{{command}}' requires at least one argument");
        }

        var verb = args.Array[args.Offset];
        if (factories.TryGetValue(verb, out var factory))
        {
            var subsegment = new ArraySegment<string>(args.Array, args.Offset + 1, args.Count - 1);
            return factory(subsegment);
        }

        throw new ArgumentException($"Invalid verb: {verb}");
    }
}

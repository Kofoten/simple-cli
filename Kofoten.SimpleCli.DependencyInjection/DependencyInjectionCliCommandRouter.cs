using System;
using System.Collections.Generic;

namespace Kofoten.SimpleCli.DependencyInjection;

public class DependencyInjectionCliCommandRouter
{
    private readonly Dictionary<string, Func<ArraySegment<string>, IServiceProvider, CliCommand>> factories = [];

    public CliCommand GetCommand(string[] args, IServiceProvider serviceProvider) => ResolveCommand(new ArraySegment<string>(args), serviceProvider);

    public void Map(string verb, Action<DependencyInjectionCliCommandRouter> configure)
    {
        var router = new DependencyInjectionCliCommandRouter();
        configure(router);
        factories.Add(verb, router.ResolveCommand);
    }

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

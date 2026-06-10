using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Kofoten.SimpleCli.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static ServiceCollection AddCliCommands(this ServiceCollection services, string[] args, Action<CliCommandRouter> configure)
    {
        var router = new CliCommandRouter();
        configure(router);

        var command = router.GetCommand(args);
        services.TryAddSingleton<ICliCommand>(factory);
        return services;
    }
}

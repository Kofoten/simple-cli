using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;

namespace Kofoten.SimpleCli.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a CLI command to the service collection. The command is resolved based on the provided arguments and the configuration of the router.
    /// </summary>
    /// <param name="services">The service collection to add the CLI command to.</param>
    /// <param name="args">The arguments to resolve the command from.</param>
    /// <param name="configure">The configuration action to define subcommands.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddCliCommands(this IServiceCollection services, string[] args, Func<IEnumerable<string>, string, int> errorHandler, Action<DependencyInjectionCliCommandRouter> configure)
    {
        var router = new DependencyInjectionCliCommandRouter(errorHandler);
        configure(router);

        services.TryAddSingleton((sp) => router.GetCommand(args, sp));
        return services;
    }
}

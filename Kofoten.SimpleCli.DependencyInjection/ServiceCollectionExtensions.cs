using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Kofoten.SimpleCli.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCliCommands(this IServiceCollection services, string[] args, Action<DependencyInjectionCliCommandRouter> configure)
    {
        var router = new DependencyInjectionCliCommandRouter();
        configure(router);

        services.TryAddSingleton((sp) => router.GetCommand(args, sp));
        return services;
    }
}

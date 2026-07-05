using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

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
    public static IServiceCollection AddCliCommands(
        this IServiceCollection services,
        string[] args,
        Action<CliCommandRouter<Func<IServiceProvider, CliParseResult>>> configure,
        Func<Exception, IServiceProvider?, int> exceptionHandler)
    {
        var router = new CliCommandRouter<Func<IServiceProvider, CliParseResult>>(exceptionHandler);
        configure.Invoke(router);

        services.TryAddSingleton((sp) => CliCommand.CreateFromFactoryFunctionResult(
            router.GetFactoryFunction(args),
            (factoryFunction) => factoryFunction.Invoke(sp),
            exceptionHandler,
            sp));

        return services;
    }
}

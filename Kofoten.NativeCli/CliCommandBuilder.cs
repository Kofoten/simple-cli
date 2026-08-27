using Kofoten.NativeCli.Internal;
using System;

namespace Kofoten.NativeCli;

/// <summary>
/// A builder for configuring and registering CLI commands. It allows you to define command routes, handle exceptions, and resolve commands based on provided arguments.
/// </summary>
public sealed class CliCommandBuilder
{
    private readonly CliCommandRouter<Func<CliParseResult>> router;
    private readonly Func<Exception, IServiceProvider?, int> exceptionHandler;

    private CliCommandBuilder(
        CliCommandRouter<Func<CliParseResult>> router,
        Func<Exception, IServiceProvider?, int> exceptionHandler)
    {
        this.router = router;
        this.exceptionHandler = exceptionHandler;
    }

    /// <summary>
    /// Resolves a command based on the provided arguments. The first argument is treated as the
    /// verb, and the remaining arguments are passed to the corresponding command factory.
    /// </summary>
    /// <param name="args">The arguments to resolve the command from.</param>
    /// <returns>The resolved command.</returns>
    public CliCommand ToCommand(string[] args)
        => CliCommand.CreateFromFactoryFunctionResult(
            router.GetFactoryFunction(args),
            (factoryFunction) => factoryFunction.Invoke(),
            exceptionHandler,
            null);

    /// <summary>
    /// Configures a new instance of <see cref="CliCommandBuilder"/>.
    /// </summary>
    /// <param name="configure">The function configuring the builder.</param>
    /// <param name="exceptionHandler">An exception handler used while resolving routes, parsing the command and running the command.</param>
    /// <returns>A new instance of <see cref="CliCommandBuilder"/>.</returns>
    public static CliCommandBuilder Configure(
        Action<CliCommandRouter<Func<CliParseResult>>> configure,
        Func<Exception, IServiceProvider?, int> exceptionHandler)
    {
        var router = new CliCommandRouter<Func<CliParseResult>>(exceptionHandler);
        configure.Invoke(router);

        return new CliCommandBuilder(router, exceptionHandler);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kofoten.NativeCli;

public sealed class CliCommand
{
    private readonly ICliParsable? command;
    private readonly CliParseException? parseException;
    private readonly Func<Exception, IServiceProvider?, int> exceptionHandler;
    private readonly IServiceProvider? serviceProvider;

    private CliCommand(ICliParsable? command, CliParseException? parseException, Func<Exception, IServiceProvider?, int> exceptionHandler, IServiceProvider? serviceProvider)
    {
        this.command = command;
        this.parseException = parseException;
        this.exceptionHandler = exceptionHandler;
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Executes the command synchronously. If the command is asynchronous, it will be executed synchronously
    /// by blocking the calling thread until completion and Ctrl + C detection is automatically configured
    /// using a <see cref="CancellationToken"/>.
    /// </summary>
    /// <returns>The exit code of the command.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type can not be invoked.</exception>
    public int Execute() => ExecuteAsync(true).GetAwaiter().GetResult();

    /// <summary>
    /// Executes the command asynchronously. Asynchronous commands will have  Ctrl + C detection is automatically configured
    /// using a <see cref="CancellationToken"/>.
    /// </summary>
    /// <returns>The exit code of the command.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type can not be invoked.</exception>
    public Task<int> ExecuteAsync() => ExecuteAsync(true);

    /// <summary>
    /// Executes the command asynchronously. This method can be used when you want to configure the
    /// <paramref name="cancellationToken"/> manually.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The exit code of the command.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type can not be invoked.</exception>
    public Task<int> ExecuteAsync(CancellationToken cancellationToken) => ExecuteAsync(false, cancellationToken);

    private async Task<int> ExecuteAsync(bool configureCancellartion, CancellationToken cancellationToken = default)
    {
        using var cts = new CancellationTokenSource();
        void handler(object _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cts.Cancel();
        }

        Console.CancelKeyPress += handler;

        var ct = configureCancellartion ? cts.Token : cancellationToken;
        try
        {
            switch (command)
            {
                case ICliCommand cliCommand:
                    return cliCommand.Execute();
                case IAsyncCliCommand asyncCliCommand:
                    return await asyncCliCommand.ExecuteAsync(ct);
                case null:
                    return parseException is null ? 0 : exceptionHandler.Invoke(parseException, serviceProvider);
                default:
                    return exceptionHandler.Invoke(new InvalidOperationException($"Unsupported command type: {command.GetType().FullName}."), serviceProvider);
            }
            ;
        }
        catch (Exception ex)
        {
            return exceptionHandler.Invoke(ex, serviceProvider);
        }
        finally
        {
            if (configureCancellartion)
            {
                Console.CancelKeyPress -= handler;
            }
        }
    }

    public static CliCommand CreateFromFactoryFunctionResult<TFactoryFunction>(
        CliFactoryFunctionResult<TFactoryFunction> factoryFunctionResolutionResult,
        Func<TFactoryFunction, CliParseResult> factoryInvoker,
        Func<Exception, IServiceProvider?, int> exceptionHandler,
        IServiceProvider? serviceProvider)
    {
        switch (factoryFunctionResolutionResult)
        {
            case CliFactoryFunctionResult<TFactoryFunction>.Success success:
                var parseResult = factoryInvoker.Invoke(success.FactoryFunction);
                return parseResult switch
                {
                    CliParseResult.Success parseSuccess => new(parseSuccess.Parsable, null, exceptionHandler, serviceProvider),
                    CliParseResult.Failure parseFailure => new(null, new CliParseException(parseFailure.Errors, success.HelpText), exceptionHandler, serviceProvider),
                    _ => throw new ArgumentOutOfRangeException()
                };
            case CliFactoryFunctionResult<TFactoryFunction>.Failure failure:
                return new(null, new CliParseException(failure.Errors, failure.HelpText), exceptionHandler, serviceProvider);
            case CliFactoryFunctionResult<TFactoryFunction>.Usage usage:
                Console.WriteLine(usage.HelpText);
                return new(null, null, exceptionHandler, serviceProvider);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

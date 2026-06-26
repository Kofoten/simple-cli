using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kofoten.SimpleCli;

public sealed class CliCommand
{
    private readonly int exitCode;
    private readonly ICliParsable? command;

    internal CliCommand(ICliParsable command)
    {
        this.command = command ?? throw new ArgumentNullException(nameof(command));
    }

    internal CliCommand(int exitCode)
    {
        command = null;
        this.exitCode = exitCode;
    }

    /// <summary>
    /// Executes the command synchronously. If the command is asynchronous, it will be executed synchronously
    /// by blocking the calling thread until completion and Ctrl + C detection is automatically configured
    /// using a <see cref="CancellationToken"/>.
    /// </summary>
    /// <returns>The exit code of the command.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type can not be invoked.</exception>
    public int Execute() => command switch
    {
        ICliCommand cliCommand => cliCommand.Execute(),
        IAsyncCliCommand asyncCliCommand => ConfigureCancellationAndExecuteAsyncCommand(asyncCliCommand).GetAwaiter().GetResult(),
        null => exitCode,
        _ => throw new InvalidOperationException($"Unsupported command type: {command.GetType().FullName}."),
    };

    /// <summary>
    /// Executes the command asynchronously. Asynchronous commands will have  Ctrl + C detection is automatically configured
    /// using a <see cref="CancellationToken"/>.
    /// </summary>
    /// <returns>The exit code of the command.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type can not be invoked.</exception>
    public Task<int> ExecuteAsync() => command switch
    {
        ICliCommand cliCommand => Task.FromResult(cliCommand.Execute()),
        IAsyncCliCommand asyncCliCommand => ConfigureCancellationAndExecuteAsyncCommand(asyncCliCommand),
        null => Task.FromResult(exitCode),
        _ => throw new InvalidOperationException($"Unsupported command type: {command.GetType().FullName}."),
    };

    /// <summary>
    /// Executes the command asynchronously. This method can be used when you want to configure the
    /// <paramref name="cancellationToken"/> manually.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The exit code of the command.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type can not be invoked.</exception>
    public Task<int> ExecuteAsync(CancellationToken cancellationToken) => command switch
    {
        ICliCommand cliCommand => Task.FromResult(cliCommand.Execute()),
        IAsyncCliCommand asyncCliCommand => asyncCliCommand.ExecuteAsync(cancellationToken),
        null => Task.FromResult(exitCode),
        _ => throw new InvalidOperationException($"Unsupported command type: {command.GetType().FullName}."),
    };

    private async Task<int> ConfigureCancellationAndExecuteAsyncCommand(IAsyncCliCommand asyncCliCommand)
    {
        using var cts = new CancellationTokenSource();
        void handler(object _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cts.Cancel();
        }

        Console.CancelKeyPress += handler;
        try
        {
            return await asyncCliCommand.ExecuteAsync(cts.Token);
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}

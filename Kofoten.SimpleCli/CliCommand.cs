using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kofoten.SimpleCli;

public sealed class CliCommand(ICliParsable command)
{
    private readonly ICliParsable command = command ?? throw new ArgumentNullException(nameof(command));

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) => command switch
    {
        ICliCommand cliCommand => Task.FromResult(cliCommand.Execute()),
        IAsyncCliCommand asyncCliCommand => asyncCliCommand.ExecuteAsync(cancellationToken),
        _ => throw new InvalidOperationException($"Unsupported command type: {command.GetType().FullName}."),
    };

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public int Execute() => command switch
    {
        ICliCommand cliCommand => cliCommand.Execute(),
        IAsyncCliCommand asyncCliCommand => ExecuteAsyncCommandSync(asyncCliCommand),
        _ => throw new InvalidOperationException($"Unsupported command type: {command.GetType().FullName}."),
    };

    private int ExecuteAsyncCommandSync(IAsyncCliCommand asyncCliCommand)
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
            return asyncCliCommand.ExecuteAsync(cts.Token).GetAwaiter().GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}

using Kofoten.SimpleCli;
using Kofoten.SimpleCli.NetstandardTest;
using System;

public class Program
{
    public static int Main(string[] args)
    {
        var imaginaryService = new object();

        try
        {
            // Let's test the happy path with the greedy collection and both boolean flags
            string[] simulatedArgs = new string[] { "10", "20", "-a", "5", "15", "--verbose", "--table" };

            Console.WriteLine($"Simulating single command app args: {string.Join(" ", simulatedArgs)}\n");

            var command = AdditionCommandParser.Parse(simulatedArgs, imaginaryService);

            // Execute your handcrafted logic!
            command.Execute();
        }
        catch (AggregateException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Command failed with the following errors:");
            foreach (var inner in ex.InnerExceptions)
            {
                Console.WriteLine($"- {inner.Message}");
            }
            Console.ResetColor();
        }

        try
        {
            // Let's test the happy path with the greedy collection and both boolean flags
            string[] simulatedArgs = new string[] { "math", "add", "10", "20", "-a", "5", "15", "--verbose", "--table" };

            Console.WriteLine($"Simulating multi command app args: {string.Join(" ", simulatedArgs)}\n");

            var builder = CliCommandBuilder.Configure(router =>
            {
                router.Map("math", sr =>
                {
                    sr.MapAdditionCommand("add", imaginaryService);
                });
            }, ExceptionHandler);

            var command = builder.ToCommand(simulatedArgs);

            // Execute your handcrafted logic!
            command.Execute();
        }
        catch (AggregateException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Command failed with the following errors:");
            foreach (var inner in ex.InnerExceptions)
            {
                Console.WriteLine($"- {inner.Message}");
            }
            Console.ResetColor();
        }

        return 0;
    }

    static int ExceptionHandler(Exception exception, IServiceProvider _)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        if (exception is CliParseException parseException)
        {
            Console.WriteLine("Command failed with the following errors:");
            foreach (var error in parseException.Errors)
            {
                Console.WriteLine($"- {error}");
            }
            Console.ResetColor();
            Console.WriteLine(parseException.HelpText);
            return 1;
        }

        Console.WriteLine(exception.Message);
        Console.ResetColor();
        Console.WriteLine(exception.StackTrace);
        return 42;
    }
}

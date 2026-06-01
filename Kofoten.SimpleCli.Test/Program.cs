using Kofoten.SimpleCli.Test;

try
{
    // Let's test the happy path with the greedy collection and both boolean flags
    string[] simulatedArgs = ["10", "20", "-a", "5", "15", "--verbose", "--table"];

    Console.WriteLine($"Simulating args: {string.Join(" ", simulatedArgs)}\n");

    var command = AdditionCommandParser.Parse(simulatedArgs);

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
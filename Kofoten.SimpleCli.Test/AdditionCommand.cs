using Kofoten.SimpleCli.Test.Data;
using System.Collections.Frozen;

namespace Kofoten.SimpleCli.Test;

/// <summary>
/// Adds numbers togheter and prints the result.
/// </summary>
internal class AdditionCommand(object imaginaryService) : BaseCommand
{
    [CliArgument(0, nameof(FirstNumber), Description = "The first number to add.")]
    public required int FirstNumber { get; init; }

    [CliArgument(1, nameof(SecondNumber), Description = "The second number to add.")]
    public required int SecondNumber { get; init; }

    [CliOption("additional-numbers", Short = 'a', Description = "Additional numbers to add.")]
    public int[] AdditionalNumbers { get; init; } = [];

    [CliOption("verbose", Short = 'V', Description = "Print the result of each addidtion.")]
    public bool Verbose { get; init; } = false;

    [CliOption("table", Short = 't', Description = "Print each step as a table.")]
    public bool Table { get; init; } = false;

    [CliOption("version", Short = 'v', Description = "Displays the version of the command")]
    public bool Version { get; init; } = false;

    [CliOption("cheese", Description = "Eats the specified cheese", Hidden = true)]
    public Cheese Cheese { get; init; } = new Cheese("Västerbotten", "Sweden");

    [CliOption("limit", Short = 'l', Description = "Sets a limit")]
    [CliParser(typeof(CliParsers), nameof(CliParsers.TryParseLimit))]
    public int Limit { get; init; } = 5;

    [CliOption("signature", Short = 's', Description = "A signature for the math")]
    [CliParser(typeof(CliParsers), nameof(CliParsers.TryParseSignature))]
    public required string Signature { get; init; }

    [CliOption("indexed-cheese")]
    public CheeseLookup IndexedCheeses { get; init; } = [];

    [CliOption("frozen-cheese")]
    public CheeseCollection FrozenCheese { get; init; } = [];

    [CliOption("header")]
    public FrozenDictionary<string, string> Header { get; init; } = new Dictionary<string, string>() { { "type", "human" }, { "role", "king" } }.ToFrozenDictionary();

    [CliOption("greetings")]
    public IEnumerable<string> Greetings { get; init; } = [];

    public override int Execute()
    {
        int[] allNumbers = [FirstNumber, SecondNumber, .. AdditionalNumbers];
        int sum = allNumbers[0];
        for (int i = 1; i < allNumbers.Length; i++)
        {
            sum += allNumbers[i];
            if (Verbose)
            {
                ReadOnlySpan<int> remaining = [];
                if (i < allNumbers.Length - 1)
                {
                    remaining = allNumbers.AsSpan(i + 1);
                }

                if (Table)
                {
                    PrintTableStepResult(sum, remaining);
                }
                else
                {
                    PrintStepResult(sum, remaining);
                }
            }
        }

        Console.WriteLine($"The sum is: {sum}");

        return 0;
    }

    internal CliValidationResult Validate()
    {
        if (FirstNumber < 0)
        {
            return new CliValidationResult.Failure([$"{nameof(FirstNumber)} must be a positive integer"]);
        }

        return new CliValidationResult.Success();
    }

    private static void PrintStepResult(int sum, ReadOnlySpan<int> remainingNumbers)
    {
        Console.Write(sum);
        for (int i = 0; i < remainingNumbers.Length; i++)
        {
            Console.Write(" + ");
            Console.Write(remainingNumbers[i]);
        }
        Console.WriteLine();
    }

    private static void PrintTableStepResult(int sum, ReadOnlySpan<int> remainingNumbers)
    {
        var maxDigits = GetBase10DigitCount(sum);
        for (int i = 0; i < remainingNumbers.Length; i++)
        {
            var digits = GetBase10DigitCount(remainingNumbers[i]);
            if (digits > maxDigits)
            {
                maxDigits = digits;
            }
        }

        var width = 1 + maxDigits;
        Console.WriteLine(sum.ToString().PadLeft(1 + width));
        for (int i = 0; i < remainingNumbers.Length; i++)
        {
            Console.Write('+');
            Console.WriteLine(remainingNumbers[i].ToString().PadLeft(width));
        }
        Console.WriteLine("".PadLeft(1 + width, '-'));
    }

    private static int GetBase10DigitCount(int number)
    {
        if (number == 0)
        {
            return 1;
        }

        var count = (int)Math.Log10(Math.Abs(number));
        if (number > 0)
        {
            return count;
        }

        return 1 + count;
    }
}

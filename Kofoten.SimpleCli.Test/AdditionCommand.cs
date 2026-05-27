namespace Kofoten.SimpleCli.Test;

internal class AdditionCommand : ICliCommand
{
    [CliArgument(0, nameof(FirstNumber), Description = "The first number to add.")]
    public required int FirstNumber { get; init; }

    [CliArgument(1, nameof(SecondNumber), Description = "The second number to add.")]
    public required int SecondNumber { get; init; }

    [CliOption("additional-numbers", Short = 'a', Description = "Additional numbers to add.")]
    public IEnumerable<int> AdditionalNumbers { get; init; } = [];

    [CliOption("verbose", Short = 'v', Description = "Print the result of each addidtion.")]
    public bool Verbose { get; init; } = false;

    [CliOption("table", Short = 't', Description = "Print each step as a table.")]
    public bool Table { get; init; } = false;

    public int Execute()
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
        for (int i = 0; i <= remainingNumbers.Length; i++)
        {
            var digits = GetBase10DigitCount(remainingNumbers[i]);
            if (digits > maxDigits)
            {
                maxDigits = digits;
            }
        }

        var width = 1 + maxDigits;
        Console.WriteLine(sum.ToString().PadLeft(1 + width));
        for (int i = 0; i <= remainingNumbers.Length; i++)
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

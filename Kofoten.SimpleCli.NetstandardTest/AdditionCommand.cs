using System;
using System.Collections.Generic;
using System.Linq;

namespace Kofoten.SimpleCli.NetstandardTest
{
    /// <summary>
    /// Adds numbers togheter and prints the result.
    /// </summary>
    public class AdditionCommand : ICliCommand
    {
        private readonly object imaginaryService;

        public AdditionCommand(object imaginaryService)
        {
            this.imaginaryService = imaginaryService;
        }

        [CliArgument(0, nameof(FirstNumber), Description = "The first number to add.")]
        public int FirstNumber { get; set; }

        [CliArgument(1, nameof(SecondNumber), Description = "The second number to add.")]
        public int SecondNumber { get; set; }

        [CliOption("additional-numbers", Short = 'a', Description = "Additional numbers to add.")]
        public IEnumerable<int> AdditionalNumbers { get; set; } = new int[0];

        [CliOption("verbose", Short = 'v', Description = "Print the result of each addidtion.")]
        public bool Verbose { get; set; } = false;

        [CliOption("table", Short = 't', Description = "Print each step as a table.")]
        public bool Table { get; set; } = false;

        internal CliValidationResult Validate()
        {
            if (FirstNumber < 0)
            {
                return new CliValidationResult.Failure(new string[] { $"{nameof(FirstNumber)} must be a positive integer" });
            }

            return new CliValidationResult.Success();
        }

        public int Execute()
        {
            int[] allNumbers = new int[] { FirstNumber, SecondNumber }.Concat(AdditionalNumbers).ToArray();
            int sum = allNumbers[0];
            for (int i = 1; i < allNumbers.Length; i++)
            {
                sum += allNumbers[i];
                if (Verbose)
                {
                    var remainingCount = allNumbers.Length - i - 1;
                    var remaining = new int[remainingCount];
                    if (i < allNumbers.Length - 1)
                    {
                        Array.Copy(allNumbers, i + 1, remaining, 0, remainingCount);
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

        private static void PrintStepResult(int sum, int[] remainingNumbers)
        {
            Console.Write(sum);
            for (int i = 0; i < remainingNumbers.Length; i++)
            {
                Console.Write(" + ");
                Console.Write(remainingNumbers[i]);
            }
            Console.WriteLine();
        }

        private static void PrintTableStepResult(int sum, int[] remainingNumbers)
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
}

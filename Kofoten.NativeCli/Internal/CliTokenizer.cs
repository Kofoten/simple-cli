using System;
using System.Collections.Generic;

namespace Kofoten.NativeCli.Internal;

public static class CliTokenizer
{
    public static IEnumerable<CliToken> Tokenize(ArraySegment<string> args, string[] knownLongOptions, char[] knownShortOptions)
    {
        for (int i = args.Offset; i < args.Array.Length; i++)
        {
            if (args.Array[i].StartsWith("--"))
            {
                if (args.Array[i] == "--")
                {
                    yield return new CliToken(CliTokenType.EndOfOptions, i, 0, 2);
                }

                if (knownLongOptions.Contains(args.Array[i]))
                {
                    yield return new CliToken(CliTokenType.KnownOption, i, 0, args.Array[i].Length);
                }
                else
                {
                    yield return new CliToken(CliTokenType.UnknownLongOption, i, 0, args.Array[i].Length);
                }
            }
            else if (args.Array[i].StartsWith("-") && args.Array[i].Length > 1)
            {
                for (int j = 1; j < args.Array[i].Length; j++)
                {
                    char option = args.Array[i][j];
                    if (knownShortOptions.Contains(option))
                    {
                        yield return new CliToken(CliTokenType.ShortOption, i, j, 1);
                    }
                    else
                    {
                        yield return new CliToken(CliTokenType.UnknownShortOption, i, j, 1);
                    }
                }
            }
            else
            {
                yield return new CliToken(CliTokenType.Value, i, 0, args.Array[i].Length);
            }
        }
    }

    private static bool OptionsEquals(int hyphenCount, string option, string knownOption)
    {
        var optionLength = option.Length - hyphenCount;
        if (optionLength != knownOption.Length)
        {
            return false;
        }

        for (int i = 0; i < optionLength; i++)
        {
            if (option[i + hyphenCount] != knownOption[i])
            {
                return false;
            }
        }

        return true;
    }
}

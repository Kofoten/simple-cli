namespace Kofoten.SimpleCli.Test;

public class HandcraftedAdditionCommandParser
{
    public AdditionCommand Parse(string[] args)
    {
        List<Exception> errors = [];

        int arg_FirstNumber = 0;
        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out arg_FirstNumber))
            {
                errors.Add(new ArgumentException("Argument FirstNumber is not an integer"));
            }
        }
        else
        {
            errors.Add(new ArgumentException("Missing required argument FirstNumber"));
        }

        int arg_SecondNumber = 0;
        if (args.Length > 1)
        {
            if (!int.TryParse(args[1], out arg_SecondNumber))
            {
                errors.Add(new ArgumentException("Argument SecondNumber is not an integer"));
            }
        }
        else
        {
            errors.Add(new ArgumentException("Missing required argument SecondNumber"));
        }

        List<int> opt_AdditionalNumbers = [];
        bool opt_Verbose = false;
        bool opt_Table = false;

        int state = 0;
        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--additional-numbers":
                case "-a":
                    state = 1;
                    continue;
                case "--verbose":
                case "-v":
                    state = 2;
                    opt_Verbose = true;
                    continue;
                case "--table":
                case "-t":
                    state = 3;
                    opt_Table = true;
                    continue;
                default:
                    break;
            }

            switch (state)
            {
                case 1:
                    {
                        if (int.TryParse(args[i], out int v))
                        {
                            opt_AdditionalNumbers.Add(v);
                        }
                        else
                        {
                            errors.Add(new ArgumentException($"Invalid integer value ({args[i]}) for option '--additional-numbers' at position {i}."));
                        }
                    }
                    break;
                case 2:
                    {
                        if (bool.TryParse(args[i], out bool v))
                        {
                            opt_Verbose = v;
                        }
                        else
                        {
                            errors.Add(new ArgumentException($"Invalid boolean value ({args[i]}) for option '--verbose' at position {i}."));
                        }
                    }
                    state = 0;
                    break;
                case 3:
                    {
                        if (bool.TryParse(args[i], out bool v))
                        {
                            opt_Table = v;
                        }
                        else
                        {
                            errors.Add(new ArgumentException($"Invalid boolean value ({args[i]}) for option '--table' at position {i}."));
                        }
                    }
                    state = 0;
                    break;
                default:
                    break;
            }
        }

        if (errors.Count == 0)
        {
            return new AdditionCommand()
            {
                FirstNumber = arg_FirstNumber,
                SecondNumber = arg_SecondNumber,
                AdditionalNumbers = opt_AdditionalNumbers,
                Verbose = opt_Verbose,
                Table = opt_Table
            };
        }

        throw new AggregateException(errors);
    }
}

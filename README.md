# Kofoten.SimpleCli

This cli library was built because I could not find anything out there which had exactly what I wanted and the developer experience I wanted. I wanted super simple DX with full support for NativeAOT and seamless integration with dependency injection (using `Kofoten.SimpleCli.DependencyInjection`). I built this specifically to solve cli parsing, nothing else. I did not want to have a super generic do it all cli tool with all the fancy features. This cli library is therefore highly opinionated and is extremely simple to use.

Example code (using dependency injection):

AdditionCommand.cs

```c#
/// <summary>
/// Adds numbers together and prints the result.
/// </summary>
public class AdditionCommand(object imaginaryService) : ICliCommand
{
    [CliArgument(0, nameof(FirstNumber), Description = "The first number to add.")]
    public required int FirstNumber { get; init; }

    [CliArgument(1, nameof(SecondNumber), Description = "The second number to add.")]
    public required int SecondNumber { get; init; }

    [CliOption("additional-numbers", Short = 'a', Description = "Additional numbers to add.")]
    public int[] AdditionalNumbers { get; init; } = [];

    [CliOption("verbose", Short = 'V', Description = "Print the result of each addition.")]
    public bool Verbose { get; init; } = false;

    public int Execute()
    {
        int[] allNumbers = [FirstNumber, SecondNumber, .. AdditionalNumbers];
        int sum = allNumbers[0];
        for (int i = 1; i < allNumbers.Length; i++)
        {
            sum += allNumbers[i];
            if (Verbose)
            {
                Console.Write(sum);
                for (int j = i + 1; j < allNumbers.Length; j++)
                {
                    Console.Write(" + ");
                    Console.Write(allNumbers[j]);
                }
                Console.WriteLine();
            }
        }

        Console.WriteLine($"The sum is: {sum}");

        return 0;
    }
}
```

Program.cs

```c#
using Kofoten.SimpleCli;
using Kofoten.SimpleCli.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

return new ServiceCollection()
    .AddSingleton(new object())
    .AddCliCommands(args, ErrorHandler, router =>
    {
        router.MapAdditionCommand("add");
    })
    .BuildServiceProvider()
    .GetRequiredService<CliCommand>()
    .Execute();

static int ErrorHandler(IEnumerable<string> errors, string helpText)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Command failed with the following errors:");
    foreach (var error in errors)
    {
        Console.WriteLine($"- {error}");
    }
    Console.ResetColor();
    Console.WriteLine(helpText);
    return 1;
}
```

## Limitations

There are some limitations in what kind of cli that can be designed. Some limitations are active decisions where others are unfortunate side effects or uncompleted features.

### Fixed limitations

- The options `-h` and `--help` are reserved to print help text.
- No rich console UI (There are many other great libraries out there and i have no interest in developing such).
- No configuration file or environment variable binding (use existing builtin features).

### May change

- Arguments must come before any options.
- Arguments are currently restricted to single value types.
- There is no detection for unknown options. Example: The only existing option is `--hello`, if the user writes `--x` nothing will happen and if the user writes `--hello --x` then `--x` will be passed as the value of `--hello`.
- No support for `-` to indicate reading from stdin.
- No support for `--` to indicate the end of options.
- No middleware or interception pipeline.

### Will change (probably)

- No validation pipeline.
- No custom parsers.
- No hidden or global options.
- No shell auto completions.

## Supported property types

### Single value types

Any type that implements a public and static method with the name `TryParse`.  
There are two possible signatures that can be used:

- `public static bool TryParse(string s, out T value)`
- `public static bool TryParse(string s, out T value, out string error)`

Implement the second version if you want to provide a specific error message to the user.

### Multi value types

Remember that the rules for [single value types](#single-value-types) apply to the item type of any collection.

**Supported types**:

- Arrays
- `System.Collections.Generic.List<T>`
- `System.Collections.Frozen.FrozenSet<T>`
- Immutable collections:
  - `System.Collections.Immutable.ImmutableArray<T>`
  - `System.Collections.Immutable.ImmutableList<T>`
  - `System.Collections.Immutable.ImmutableHashSet<T>`
- Any of the following interfaces (Note: the backing type for the interfaces will be `System.Collections.Generic.List<T>`)
  - `System.Collections.Generic.IEnumerable<T>`
  - `System.Collections.Generic.ICollection<T>`
  - `System.Collections.Generic.IReadOnlyCollection<T>`
  - `System.Collections.Generic.IList<T>`
  - `System.Collections.Generic.IReadOnlyList<T>`
- **Any** type that implements `System.Collections.Generic.IEnumerable<T>` and has a public constructor that takes a single parameter of type `System.Collections.Generic.IEnumerable<T>`

### Key value pair types

Key value pairs are passed using the equals sign as the delimiter. Example `--headers Accept=text/html`
Remember that the rules for [single value types](#single-value-types) apply to both the key and value type.

- `System.Collections.Generic.Dictionary<TKey, TValue>`
- `System.Collections.Frozen.FrozenDictionary<TKey, TValue>`
- `System.Collections.Immutable.ImmutableDictionary<TKey, TValue>`
- Any of the following dictionary interfaces (Note: the backing type for the interfaces will be `System.Collections.Generic.Dictionary<TKey, TValue>`)
  - `System.Collections.Generic.IDictionary<TKey, TValue>`
  - `System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>`
- Arrays of `System.Collections.Generic.KeyValuePair<TKey, TValue>`
- `System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<TKey, TValue>>`
- `System.Collections.Frozen.FrozenSet<System.Collections.Generic.KeyValuePair<TKey, TValue>>`
- Immutable collections:
  - `System.Collections.Immutable.ImmutableArray<System.Collections.Generic.KeyValuePair<TKey, TValue>>`
  - `System.Collections.Immutable.ImmutableList<System.Collections.Generic.KeyValuePair<TKey, TValue>>`
  - `System.Collections.Immutable.ImmutableHashSet<System.Collections.Generic.KeyValuePair<TKey, TValue>>`
- Any of the following collection interfaces (Note: the backing type for the interfaces will be `System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<TKey, TValue>>`)
  - `System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<TKey, TValue>>`
  - `System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<TKey, TValue>>`
  - `System.Collections.Generic.IReadOnlyCollection<System.Collections.Generic.KeyValuePair<TKey, TValue>>`
  - `System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<TKey, TValue>>`
  - `System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<TKey, TValue>>`
- **Any** type that implements `System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<TKey, TValue>>` and has a public constructor that takes a single parameter of type `System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<TKey, TValue>>`

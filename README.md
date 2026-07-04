# Kofoten.SimpleCli

This cli library was built because I could not find anything out there which had exactly what I wanted and the developer experience I wanted. I wanted super simple DX with full support for NativeAOT and seamless integration with dependency injection (using `Kofoten.SimpleCli.DependencyInjection`). I built this specifically to solve cli parsing, nothing else. I did not want to have a super generic do it all cli tool with all the fancy features. This cli library is therefore highly opinionated and is extremely simple to use.

## Usage

This library is designed to be incredibly easy to use and requires minimal effort to get up and running.

### Example implementation

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

Program.cs (single command app)

```c#
using Kofoten.SimpleCli;

return AdditionCommandParser.Parse(args, new()).Execute();
```

Program.cs (with subcommand routing)

```c#
using Kofoten.SimpleCli;

var router = new CliCommandRouter(ErrorHandler);
router.Map("math", sr =>
{
    sr.MapAdditionCommand("add", new());
});

return router.GetCommand(args).Execute();

static int ExceptionHandler(Exception exception, IServiceProvider? _)
{
    if (exception is CliParseException parseException)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Command failed with the following errors:");
        foreach (var error in parseException.Errors)
        {
            Console.WriteLine($"- {error}");
        }
        Console.ResetColor();
        Console.WriteLine(parseException.HelpText);

        return 1;
    }

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("An unknown error occurred:");
    Console.WriteLine(exception.Message);
    Console.ResetColor();
    Console.WriteLine(exception.StackTrace);

    return 42;
}
```

Program.cs (using dependency injection)

```c#
using Kofoten.SimpleCli;
using Kofoten.SimpleCli.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

return new ServiceCollection()
    .AddSingleton(new object())
    .AddLogging(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Information);
    })
    .AddCliCommands(args, ErrorHandler, router =>
    {
        router.MapAdditionCommand("add");
    })
    .BuildServiceProvider()
    .GetRequiredService<CliCommand>()
    .Execute();

static int ExceptionHandler(Exception exception, IServiceProvider? sp)
{
    var logger = sp?.GetService<ILogger<Program>>();

    if (exception is CliParseException parseException)
    {
        if (logger is not null)
        {
            logger.FailedToParseArguments(parseException.Message);
        }
        else
        {
            Console.WriteLine(parseException.Message);
        }

        return 1;
    }

    if (logger is not null)
    {
        logger.UnhandledException(exception);
    }
    else
    {
        Console.WriteLine(exception.ToString());
    }

    return 42;
}
```

## Supported property types

### Single value types

Any type that implements a public and static method with the name `TryParse`.  
There are two possible signatures that can be used:

- `public static bool TryParse(string s, out T value)`
- `public static bool TryParse(string s, out T value, out string error)`

Implement the second version if you want to provide a specific error message to the user.

#### Custom parsing

If you are trying to parse external types that you can not simple add a `TryParse` method to you can use the `CliParserAttribute` to point the source generation to a specific method that should be used for parsing. These methods must have the same signature ase previously mentioned `TryParse` methods.

⚠️ This is not applicable to multi value or key value pair options. Instead the specified parser will be applied to the elements and values of these types. To define a custom key parser, use the `CliKeyParserAttribute`. The only way to achive custom multi value parsing is to create a type that uses the standard `TryParse` method and it **MUST NOT** implement the `IEnumerable<T>` interface. and values can not be passed seperatley.

Example:

```c#
public static class CustomCliParsers
{
    public static bool TryParseExtrenalType(string s, [NotNullWhen(true)] out ExternalType? v, [NotNullWhen(false)] out string? error)
    {
        var parts = s.Split('|');
        if (parts.All(p => p.Length > 0 && char.IsUpper(p[0])))
        {
            error = null;
            v = new ExternalType(parts);
            return true;
        }

        error = $"All parts must start with an upper case character.";
        v = null;
        return false;
    }
}
```

```c#
[CliOption("named-parts", Description = "Sets the parts to use for building something cool.")]
[CliParser(typeof(CliParsers), nameof(CliParsers.TryParseExtrenalType))]
public required ExternalType NamedParts { get; init; }
```

#### Enums

Standard C# `enum` types are natively supported and do not require a custom `TryParse` method. Furthermore, enums marked with the `[Flags]` attribute are fully supported, allowing users to pass the option multiple times to combine bitwise flags automatically.

⚠️ Avoid adding a flags enum as a multi value property (`IEnumerable<YourFlagsEnum>`) since that may result in undefined behaviour.

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

**Supported types**:

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

## Limitations

There are some limitations in what kind of cli that can be designed. Some limitations are active design decisions where others are unfortunate side effects or uncompleted features.

### Firm constraints

- The options `-h` and `--help` are reserved to print help text.
- No rich console UI (There are many other great libraries out there and i have no interest in developing such).
- No configuration file or environment variable binding (use existing builtin features).
- Use of `--` is required to begin parsing arguments **after** options have been passed and no more options may follow, evrything after `--` is treated as arguments.

### May change

- Arguments are restricted to single value types.
- There is no detection for unknown options. Example: The only existing option is `--hello`, if the user writes `--x` nothing will happen and if the user writes `--hello --x` then `--x` will be passed as the value of `--hello`.
- No support for `-` to indicate reading from stdin.
- No middleware or interception pipeline.
- No global options
- No combined flags, you can not combine short names like `-v`, `-y`, `-k` to `-vyk`.

### Will change (probably)

- No validation pipeline (currently you can use custom parsers to hook into the parsing pipeline using the `CliParserAttribute` and the error message out signature for custom validation).
- No shell auto completions.

## Analyzer diagnostic codes

To ensure a smooth developer experience, `Kofoten.SimpleCli` includes a Roslyn analyzer that catches configuration errors at compile time.

| Code | Title | Description | Severity |
| :--- | :--- | :--- | :--- |
| **SCLI001** | Invalid constructor count | The command class must declare exactly one public constructor to be CLI-parsable. | Error |
| **SCLI002** | Unsupported collection type | A property is using a collection type whose element type could not be resolved by the generator. | Error |
| **SCLI003** | Duplicate argument position | Multiple properties are marked with `[CliArgument]` using the same positional index. | Error |
| **SCLI004** | Duplicate option name | Multiple properties are marked with `[CliOption]` using the same name. | Error |
| **SCLI005** | Duplicate short option | Multiple properties are marked with `[CliOption]` using the same short character (e.g., `-a`). | Error |
| **SCLI006** | Reserved option name | A property attempted to use `-h` or `--help`, which are strictly reserved by the CLI router for displaying help text. | Error |
| **SCLI007** | Unsupported collection type | A property is using a collection type that is not supported by the generator. | Error |
| **SCLI008** | Ambiguous CLI property binding | A property is marked with both `[CliArgument]` and `[CliOption]`, which is not allowed. | Error |
| **SCLI009** | Missing parser | The type of a CLI property does not have a valid parser (e.g., no compatible `TryParse` method or `[CliParser]` attribute). | Error |

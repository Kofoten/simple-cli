# Kofoten.NativeCli

This cli library was built because I could not find anything out there which had exactly what I wanted and the developer experience I wanted. I wanted super simple DX with full support for NativeAOT and seamless integration with dependency injection (using `Kofoten.NativeCli.DependencyInjection`). I built this specifically to solve cli parsing, nothing else. I did not want to have a super generic do it all cli tool with all the fancy features. This cli library is therefore highly opinionated and is extremely simple to use.

## Usage

This library is designed to be incredibly easy to use and requires minimal effort to get up and running.

### Validation

There is no declarative validation. However you can implement a method named `Validate` which takes no parameters and returns a `CliValidationResult` to add validation such as ensuring strings match a specific regex or an integer falls in a specified interval. This method **may not** be abstract, async or static and **must** be public or internal. This method is called after instantiating the command, however if a failure is returned it is treated as a parsing failure and will invoke the exception handler with a `CliParseException` or throw if the `Parse` method was called directly without using the `CliCommandBuilder`.

### Inheritance

The source generator will traverse the inheritance tree and find all arguments and options as well as any validation method implemented in any base class.

### Example implementation

BaseCommand.cs

```c#
public abstract class BaseCommand : ICliCommand
{
    [CliOption("verbose", Short = 'V', Description = "Print the result of each addition.")]
    public bool Verbose { get; init; } = false;

    public abstract int Execute();
}
```

AdditionCommand.cs

```c#
/// <summary>
/// Adds numbers together and prints the result.
/// </summary>
public class AdditionCommand(object imaginaryService) : BaseCommand
{
    [CliArgument(0, nameof(FirstNumber), Description = "The first number to add.")]
    public required int FirstNumber { get; init; }

    [CliArgument(1, nameof(SecondNumber), Description = "The second number to add.")]
    public required int SecondNumber { get; init; }

    [CliOption("additional-numbers", Short = 'a', Description = "Additional numbers to add.")]
    public int[] AdditionalNumbers { get; init; } = [];

    public CliValidationResult Validate()
    {
        if (FirstNumber < 0)
        {
            return new CliValidationResult.Failure([$"{nameof(FirstNumber)} must be a positive integer"]);
        }

        return new CliValidationResult.Success();
    }

    public override int Execute()
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
using Kofoten.NativeCli;

return AdditionCommandParser.Parse(args, new()).Execute();
```

Program.cs (with subcommand routing)

```c#
using Kofoten.NativeCli;

var builder = CliCommandBuilder.Configure(router =>
{
    router.Map("math", sr =>
    {
        sr.MapAdditionCommand("add", new());
    });
}, ExceptionHandler);

return builder.ToCommand(args).Execute();

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
using Kofoten.NativeCli;
using Kofoten.NativeCli.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

return new ServiceCollection()
    .AddSingleton(new object())
    .AddLogging(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Information);
    })
    .AddCliCommands(args, router =>
    {
        router.MapAdditionCommand("add");
    }, ExceptionHandler)
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

### Implicit values

Any single value **option** can have an implicit value. This value is automatically assigned if the user passes the option flag without providing an explicit value next to it.

Note: This is different from a standard default value, which is applied when the user omits the option entirely.

You can configure this by providing the value's string representation to the `ImplicitValue` property on the `[CliOption]` attribute.

This is incredibly useful for opt-in features like a `--porcelain` flag, where you want to enable a specific behavior but don't want to force the user to type out the exact version unless they need to. In the example below, running `myapp --porcelain` will be treated exactly as if the user had typed `myapp --porcelain v1`. If the flag is omitted entirely, the property will retain its default value (`null` in this case).

```c#
[CliOption("porcelain", Description = "Prints the output in a machine-readable format", ImplicitValue = "v1")]
public string? PorcelainVersion { get; init; }
```

- `myapp --porcelain v2` $\rightarrow$ `PorcelainVersion` is "v2"
- `myapp --porcelain v1` $\rightarrow$ `PorcelainVersion` is "v1" (Explicit)
- `myapp --porcelain` $\rightarrow$ `PorcelainVersion` is "v1" (Implicit)
- `myapp` $\rightarrow$ `PorcelainVersion` is null (Default)

#### Custom parsing

If you are trying to parse external types that you can not simply add a `TryParse` method to you can use the `CliParserAttribute` to point the source generation to a specific method that should be used for parsing. These methods must have the same signature as previously mentioned `TryParse` methods.

⚠️ This is not applicable to multi value or key value pair options. Instead the specified parser will be applied to the elements and values of these types. To define a custom key parser, use the `CliKeyParserAttribute`. The only way to achieve custom multi value parsing is to create a type that uses the standard `TryParse` method and it **MUST NOT** implement the `IEnumerable<T>` interface, and values cannot be passed separately.

Example:

```c#
public static class CustomCliParsers
{
    public static bool TryParseExternalType(string s, [NotNullWhen(true)] out ExternalType? v, [NotNullWhen(false)] out string? error)
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
[CliParser(typeof(CliParsers), nameof(CliParsers.TryParseExternalType))]
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
- Use of `--` is required to begin parsing arguments **after** options have been passed and no more options may follow, everything after `--` is treated as arguments.

### May change

- Arguments are restricted to single value types.
- There is no detection for unknown options. Example: The only existing option is `--hello`, if the user writes `--x` nothing will happen and if the user writes `--hello --x` then `--x` will be passed as the value of `--hello`.
- No support for `-` to indicate reading from stdin.
- No middleware or interception pipeline.
- No global options (currently you can use an abstract `BaseCommand` to achieve similar functionality).
- No combined flags, you can not combine short names like `-v`, `-y`, `-k` to `-vyk`.
- Support for custom help text formatters.
- Optional default version querying via `-v` and `--version` (app scoped, meaning top level router only).
- No shell auto completions.
- Does **not** support the key value option pattern, meaning `--name=Kofoten` is not supported. The value of an option must be separated from the option name using a space (`--name Kofoten`).
- Does **not** support the comma separated multi value option pattern, meaning `--names Kofoten,Rasmus` is not supported except for flag enum values. The values must be separated using a space (`--names Kofoten Rasmus`).

### Will change (probably)

- Improved help text (include application name and version)
- Better default value text resolution (for types that are not constant and item, key and value types for collections and dictionaries may cause ugly help text)

## Analyzer diagnostic codes

To ensure a smooth developer experience, `Kofoten.NativeCli` includes a Roslyn analyzer that catches configuration errors at compile time.

| Code | Title | Description | Severity |
| :--- | :--- | :--- | :--- |
| **NCLI001** | Invalid constructor count | The command class must declare exactly one public constructor to be CLI-parsable. | Error |
| **NCLI002** | Unsupported collection type | A property is using a collection type whose element type could not be resolved by the generator. | Error |
| **NCLI003** | Duplicate argument position | Multiple properties are marked with `[CliArgument]` using the same positional index. | Error |
| **NCLI004** | Duplicate option name | Multiple properties are marked with `[CliOption]` using the same name. | Error |
| **NCLI005** | Duplicate short option | Multiple properties are marked with `[CliOption]` using the same short character (e.g., `-a`). | Error |
| **NCLI006** | Reserved option name | A property attempted to use `-h` or `--help`, which are strictly reserved by the CLI router for displaying help text. | Error |
| **NCLI007** | Unsupported collection type | A property is using a collection type that is not supported by the generator. | Error |
| **NCLI008** | Ambiguous CLI property binding | A property is marked with both `[CliArgument]` and `[CliOption]`, which is not allowed. | Error |
| **NCLI009** | Missing parser | The type of a CLI property does not have a valid parser (e.g., no compatible `TryParse` method or `[CliParser]` attribute). | Error |
| **NCLI010** | Invalid command accessibility | The command class must be declared as public or internal. | Error |
| **NCLI011** | Redundant default value | A required argument or option property should not have a default value assigned. | Warning |

# Kofoten.SimpleCli

This cli library was built because I could not find anything out there which had exactly what I wanted and the developer experience I wanted. I wanted super simple DX with full support for NativeAOT and seamless integration with dependency injection (using `Kofoten.SimpleCli.DependencyInjection`). I built this specifically to solve cli parsing, nothing else. I did not want to have a super generic do it all cli tool with all the fancy features. This cli library is therefore highly opinionated and is extremely simple to use.

Example code (using dependency injection):

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

- Poor key/value support (currently only supports ``System.Collections.Generic.Dictionary`2``)
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
- ``System.Collections.Generic.List`1``
- ``System.Collections.Frozen.FrozenSet`1``
- **Any** type that implements ``System.Collections.Generic.IEnumerable`1`` and has a public constructor that takes a single parameter of type ``System.Collections.Generic.IEnumerable`1``
- Any of the following interfaces (Note: the backing type for the interfaces will be ``System.Collections.Generic.List`1``)
  - ``System.Collections.Generic.IEnumerable`1``
  - ``System.Collections.Generic.ICollection`1``
  - ``System.Collections.Generic.IReadOnlyCollection`1``
  - ``System.Collections.Generic.IList`1``
  - ``System.Collections.Generic.IReadOnlyList`1``
- Immutable collections:
  - ``System.Collections.Immutable.ImmutableArray`1``
  - ``System.Collections.Immutable.ImmutableList`1``
  - ``System.Collections.Immutable.ImmutableHashSet`1``

### Key value pair types

Remember that the rules for [single value types](#single-value-types) apply to both the key and value type.

- ``System.Collections.Generic.Dictionary`2``

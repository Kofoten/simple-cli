using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Kofoten.SimpleCli.Generator.Data;

internal record SimpleCliCompilationContext(
    INamedTypeSymbol? CliParsableSymbol,
    INamedTypeSymbol? CliValidationResultSymbol,
    INamedTypeSymbol? CliArgumentAttributeSymbol,
    INamedTypeSymbol? CliOptionAttributeSymbol,
    INamedTypeSymbol? CliParserAttributeSymbol,
    INamedTypeSymbol? CliKeyParserAttributeSymbol,
    INamedTypeSymbol? CliFlagsAttributeSymbol,
    INamedTypeSymbol? EnumerableOfTSymbol,
    INamedTypeSymbol? KeyValuePairOfT2Symbol,
    INamedTypeSymbol? ListOfTSymbol,
    INamedTypeSymbol? DictionaryOfKVSymbol,
    INamedTypeSymbol? ImmutableArrayOfTSymbol,
    INamedTypeSymbol? ImmutableListOfTSymbol,
    INamedTypeSymbol? ImmutableHashSetOfTSymbol,
    INamedTypeSymbol? ImmutableDictionaryOfKVSymbol,
    INamedTypeSymbol? FrozenSetOfTSymbol,
    INamedTypeSymbol? FrozenDictionaryOfKVSymbol,
    IEnumerable<INamedTypeSymbol> SupportedEnumerableOfTInterfaceSymbols,
    IEnumerable<INamedTypeSymbol> SupportedDictionaryOfKVInterfaceSymbols,
    bool HasDependencyInjection);

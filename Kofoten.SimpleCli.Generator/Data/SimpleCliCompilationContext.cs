using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Kofoten.SimpleCli.Generator.Data;

public record SimpleCliCompilationContext(
    INamedTypeSymbol? CliParsableSymbol,
    INamedTypeSymbol? CliArgumentAttributeSymbol,
    INamedTypeSymbol? CliOptionAttributeSymbol,
    INamedTypeSymbol? FlagsAttributeSymbol,
    INamedTypeSymbol? EnumerableOfTSymbol,
    INamedTypeSymbol? KeyValuePairOfT2Symbol,
    INamedTypeSymbol? ListOfTSymbol,
    INamedTypeSymbol? ImmutableArrayOfTSymbol,
    INamedTypeSymbol? ImmutableListOfTSymbol,
    INamedTypeSymbol? ImmutableHashSetOfTSymbol,
    INamedTypeSymbol? FrozenSetOfTSymbol,
    IEnumerable<INamedTypeSymbol> SupportedEnumerableOfTInterfaceSymbols,
    bool HasDependencyInjection);

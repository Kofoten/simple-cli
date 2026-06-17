using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Kofoten.SimpleCli.Generator.Data;

internal readonly record struct CommandGenerationResult(
    CommandModel? Command,
    ImmutableArray<Diagnostic> Diagnostics);
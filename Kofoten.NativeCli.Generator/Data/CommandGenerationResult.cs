using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Kofoten.NativeCli.Generator.Data;

internal readonly record struct CommandGenerationResult(
    CommandModel? Command,
    ImmutableArray<Diagnostic> Diagnostics);
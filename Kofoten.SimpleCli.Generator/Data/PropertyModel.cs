using Microsoft.CodeAnalysis;

namespace Kofoten.SimpleCli.Generator.Data;

internal abstract record PropertyModel(
    string Name,
    string TypeName,
    string ValueTypeName,
    string? KeyTypeName,
    SpecialType SpecialType,
    bool IsRequired,
    bool IsCollection,
    bool IsDictionary,
    bool IsEnum,
    bool IsFlagsEnum,
    string Description,
    string ParseMethodName,
    bool HasErrorMessageOut);

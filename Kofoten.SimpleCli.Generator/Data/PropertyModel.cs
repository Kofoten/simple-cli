using Microsoft.CodeAnalysis;

namespace Kofoten.SimpleCli.Generator.Data;

internal abstract record PropertyModel(
    string Name,
    string TypeName,
    string ValueTypeName,
    string? KeyTypeName,
    SpecialType SpecialType,
    SpecialType KeySpecialType,
    SpecialType ValueSpecialType,
    bool IsRequired,
    bool IsCollection,
    CollectionType CollectionType,
    bool IsDictionary,
    bool IsEnum,
    bool IsFlagsEnum,
    string Description,
    string ValueParseMethodName,
    bool ValueHasErrorMessageOut,
    string? KeyParseMethodName,
    bool KeyHasErrorMessageOut);

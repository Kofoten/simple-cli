using Microsoft.CodeAnalysis;

namespace Kofoten.SimpleCli.Generator.Data;

internal record OptionPropertyModel(
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
    string ValueParseMethodName,
    bool ValueHasErrorMessageOut,
    string? KeyParseMethodName,
    bool KeyHasErrorMessageOut,
    string? DefaultValueString,
    string OptionName,
    char? ShortName
) : PropertyModel(
    Name,
    TypeName,
    ValueTypeName,
    KeyTypeName,
    SpecialType,
    IsRequired,
    IsCollection,
    IsDictionary,
    IsEnum,
    IsFlagsEnum,
    Description,
    ValueParseMethodName,
    ValueHasErrorMessageOut,
    KeyParseMethodName,
    KeyHasErrorMessageOut);
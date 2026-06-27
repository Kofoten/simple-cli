using Microsoft.CodeAnalysis;

namespace Kofoten.SimpleCli.Generator.Data;

internal record OptionPropertyModel(
    string Name,
    string TypeName,
    string ParseTypeName,
    SpecialType SpecialType,
    bool IsRequired,
    bool IsCollection,
    bool IsDictionary,
    bool IsEnum,
    string Description,
    string ParseMethodName,
    bool HasErrorMessageOut,
    string? DefaultValueString,
    string OptionName,
    char? ShortName
) : PropertyModel(
    Name,
    TypeName,
    ParseTypeName,
    SpecialType,
    IsRequired,
    IsCollection,
    IsDictionary,
    IsEnum,
    Description,
    ParseMethodName,
    HasErrorMessageOut);
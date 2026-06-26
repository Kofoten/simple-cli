using Microsoft.CodeAnalysis;

namespace Kofoten.SimpleCli.Generator.Data;

internal record OptionPropertyModel(
    string Name,
    string TypeName,
    string ParseTypeName,
    SpecialType SpecialType,
    bool IsRequired,
    bool IsCollection,
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
    Description,
    ParseMethodName,
    HasErrorMessageOut);
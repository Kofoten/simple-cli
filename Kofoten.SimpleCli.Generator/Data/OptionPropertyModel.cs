using Microsoft.CodeAnalysis;

namespace Kofoten.SimpleCli.Generator.Data;

internal record OptionPropertyModel(
    string Name,
    string TypeName,
    string ParseTypeName,
    SpecialType SpecialType,
    bool IsRequired,
    bool IsCollection,
    string ParseMethodName,
    bool HasErrorMessageOut,
    string? OptionName,
    char? ShortName
) : PropertyModel(
    Name,
    TypeName,
    ParseTypeName,
    SpecialType,
    IsRequired,
    IsCollection,
    ParseMethodName,
    HasErrorMessageOut);
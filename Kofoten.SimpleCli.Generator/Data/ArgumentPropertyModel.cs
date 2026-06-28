using Microsoft.CodeAnalysis;

namespace Kofoten.SimpleCli.Generator.Data;

internal record ArgumentPropertyModel(
    string Name,
    string TypeName,
    string ValueTypeName,
    SpecialType SpecialType,
    bool IsRequired,
    bool IsEnum,
    string Description,
    string ValueParseMethodName,
    bool ValueHasErrorMessageOut,
    int Position
) : PropertyModel(
    Name,
    TypeName,
    ValueTypeName,
    null,
    SpecialType,
    IsRequired,
    false,
    false,
    IsEnum,
    false,
    Description,
    ValueParseMethodName,
    ValueHasErrorMessageOut,
    null,
    false);
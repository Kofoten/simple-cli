using Microsoft.CodeAnalysis;

namespace Kofoten.SimpleCli.Generator.Data;

internal record ArgumentPropertyModel(
    string Name,
    string TypeName,
    string ParseTypeName,
    SpecialType SpecialType,
    bool IsRequired,
    string ParseMethodName,
    bool HasErrorMessageOut,
    int Position
) : PropertyModel(
    Name,
    TypeName,
    ParseTypeName,
    SpecialType,
    IsRequired,
    false,
    ParseMethodName,
    HasErrorMessageOut);
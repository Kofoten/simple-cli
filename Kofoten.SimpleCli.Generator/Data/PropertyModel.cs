using Microsoft.CodeAnalysis;

namespace Kofoten.SimpleCli.Generator.Data;

internal abstract record PropertyModel(
    string Name,
    string TypeName,
    string ParseTypeName,
    SpecialType SpecialType,
    bool IsRequired,
    bool IsCollection,
    string ParseMethodName,
    bool HasErrorMessageOut);

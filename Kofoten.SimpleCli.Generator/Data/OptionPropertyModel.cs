namespace Kofoten.SimpleCli.Generator.Data;

internal record OptionPropertyModel(
    string Name,
    string TypeName,
    string ParseTypeName,
    bool IsRequired,
    bool IsCollection,
    string? OptionName,
    char? ShortName
) : PropertyModel(
    Name,
    TypeName,
    ParseTypeName,
    IsRequired,
    IsCollection
);
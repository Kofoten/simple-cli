namespace Kofoten.SimpleCli.Generator.Data;

internal record ArgumentPropertyModel(
    string Name,
    string TypeName,
    string ParseTypeName,
    bool IsRequired,
    int Position
) : PropertyModel(
    Name,
    TypeName,
    ParseTypeName,
    IsRequired,
    false
);

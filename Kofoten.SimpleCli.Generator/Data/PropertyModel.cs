namespace Kofoten.SimpleCli.Generator.Data;

internal abstract record PropertyModel(
    string Name,
    string TypeName,
    string ParseTypeName,
    bool IsRequired,
    bool IsCollection
);

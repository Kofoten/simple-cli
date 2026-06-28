namespace Kofoten.SimpleCli.Generator.Data;

public enum CollectionType
{
    None = 0,
    Array,
    ListCompatible,
    ConstructorCompatible,
    ImmutableArray,
    ImmutableList,
    ImmutableHashSet,
    FrozenSet,
}

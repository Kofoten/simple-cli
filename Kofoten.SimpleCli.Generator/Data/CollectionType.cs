namespace Kofoten.SimpleCli.Generator.Data;

public enum CollectionType
{
    None = 0,
    Array,
    ListCompatible,
    DictionaryCompatible,
    ConstructorCompatible,
    ImmutableArray,
    ImmutableList,
    ImmutableHashSet,
    ImmutableDictionary,
    FrozenSet,
    FrozenDictionary,
}

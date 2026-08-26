namespace Kofoten.NativeCli.Generator.Data;

internal enum CollectionType
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

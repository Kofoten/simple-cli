using System.Collections.Generic;

namespace Kofoten.SimpleCli;

public static class CliUtilities
{
    public static Dictionary<TKey, TValue> CreateDictionaryWithOverwrite<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> pairs)
    {
        var result = new Dictionary<TKey, TValue>();
        foreach (var pair in pairs)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }
}

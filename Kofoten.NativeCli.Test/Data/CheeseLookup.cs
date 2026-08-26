using System.Collections;
using System.Collections.Frozen;

namespace Kofoten.NativeCli.Test.Data;

public class CheeseLookup(IEnumerable<KeyValuePair<int, Cheese>> cheeses) : IEnumerable<KeyValuePair<int, Cheese>>
{
    private readonly FrozenDictionary<int, Cheese> cheeses = FrozenDictionary.ToFrozenDictionary<int, Cheese>(cheeses);

    public CheeseLookup()
        : this([])
    {
    }

    public Cheese this[int key] => cheeses[key];

    public IEnumerator<KeyValuePair<int, Cheese>> GetEnumerator()
    {
        return cheeses.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

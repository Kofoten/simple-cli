using System.Collections;
using System.Collections.Frozen;

namespace Kofoten.SimpleCli.Test.Data;

public class CheeseCollection(IEnumerable<Cheese> cheeses) : IEnumerable<Cheese>
{
    private readonly FrozenSet<Cheese> cheeses = FrozenSet.ToFrozenSet(cheeses);

    public CheeseCollection()
        : this([])
    {
    }

    public IEnumerator<Cheese> GetEnumerator()
    {
        return cheeses.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

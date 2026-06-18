using System;

namespace Kofoten.SimpleCli;

public interface ICliCommandFactory<TFactoryFunction>
{
    bool IsLeaf { get; }
    string CommandDescription { get; }
    TFactoryFunction GetFactoryFunction(ArraySegment<string> args);
    string GetUsage(string commandPath);
}

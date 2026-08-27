using System;
using System.ComponentModel;

namespace Kofoten.NativeCli.Internal;

/// <summary>
/// A factory interface for creating CLI commands or parsers. It provides methods to check if the command is a leaf, get the command description, and retrieve the factory function based on provided arguments.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ICliCommandFactory<TFactoryFunction>
{
    bool IsLeaf { get; }
    string CommandDescription { get; }
    TFactoryFunction GetFactoryFunction(ArraySegment<string> args);
    string GetUsage(string commandPath);
}

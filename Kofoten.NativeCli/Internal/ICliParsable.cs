using System.ComponentModel;

namespace Kofoten.NativeCli.Internal;

/// <summary>
/// This interface is used to mark classes that can be parsed from command-line arguments. It is intended for internal use by the Kofoten.NativeCli library and should not be implemented directly by user code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ICliParsable
{
}

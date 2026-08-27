using System.Collections.Generic;
using System.ComponentModel;

namespace Kofoten.NativeCli.Internal;

/// <summary>
/// Represents the result of parsing command-line arguments into a CLI parsable object. It can either be a success, containing the parsed object, or a failure, containing a list of error messages.
/// </summary>
/// <remarks>
/// This class is intended for use by the generated parser and should not be used directly by user code.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract record CliParseResult
{
    internal CliParseResult()
    {
    }

    public sealed record Success : CliParseResult
    {
        public ICliParsable Parsable { get; private set; }

        public Success(ICliParsable parsable)
            : base()
        {
            Parsable = parsable;
        }
    }

    public sealed record Failure : CliParseResult
    {
        public IEnumerable<string> Errors { get; private set; }

        public Failure(IEnumerable<string> errors)
            : base()
        {
            Errors = errors;
        }
    }
}


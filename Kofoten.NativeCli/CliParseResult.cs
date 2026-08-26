using System.Collections.Generic;

namespace Kofoten.NativeCli;

public abstract record CliParseResult
{
    protected internal CliParseResult()
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


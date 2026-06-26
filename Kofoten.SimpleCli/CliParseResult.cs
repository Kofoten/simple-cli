using System.Collections.Generic;

namespace Kofoten.SimpleCli;

public record CliParseResult
{
    protected CliParseResult()
    {
    }

    public record Success : CliParseResult
    {
        public ICliParsable Parsable { get; private set; }

        public Success(ICliParsable parsable)
            : base()
        {
            Parsable = parsable;
        }
    }

    public record Failure : CliParseResult
    {
        public IEnumerable<string> Errors { get; private set; }

        public Failure(IEnumerable<string> errors)
            : base()
        {
            Errors = errors;
        }
    }
}


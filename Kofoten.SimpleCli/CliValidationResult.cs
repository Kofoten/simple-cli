using System.Collections.Generic;

namespace Kofoten.SimpleCli;

public abstract record CliValidationResult
{
    protected internal CliValidationResult()
    {
    }

    public sealed record Success : CliValidationResult
    {
        public Success()
            : base()
        {
        }
    }

    public sealed record Failure : CliValidationResult
    {
        public IEnumerable<string> Errors { get; private set; }

        public Failure(IEnumerable<string> errors)
            : base()
        {
            Errors = errors;
        }
    }
}

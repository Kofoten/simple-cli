using System.Collections.Generic;

namespace Kofoten.SimpleCli;

public abstract record CliFactoryFunctionResult<TFactoryFunction>
{
    public string HelpText { get; private set; }

    protected internal CliFactoryFunctionResult(string helpText)
    {
        HelpText = helpText;
    }

    public sealed record Success : CliFactoryFunctionResult<TFactoryFunction>
    {
        public TFactoryFunction FactoryFunction { get; private set; }

        internal Success(TFactoryFunction factoryFunction)
            : base(string.Empty)
        {
            FactoryFunction = factoryFunction;
        }
    }

    public sealed record Failure : CliFactoryFunctionResult<TFactoryFunction>
    {
        public IEnumerable<string> Errors { get; private set; }

        internal Failure(IEnumerable<string> errors, string helpText)
            : base(helpText)
        {
            Errors = errors;
        }
    }

    public sealed record Usage : CliFactoryFunctionResult<TFactoryFunction>
    {
        internal Usage(string helpText)
            : base(helpText)
        {
        }
    }
}


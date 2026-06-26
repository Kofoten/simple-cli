using System.Collections.Generic;

namespace Kofoten.SimpleCli;

public record CliFactoryFunctionResult<TFactoryFunction>
{
    public string HelpText { get; private set; }

    protected CliFactoryFunctionResult(string helpText)
    {
        HelpText = helpText;
    }

    public record Success : CliFactoryFunctionResult<TFactoryFunction>
    {
        public TFactoryFunction FactoryFunction { get; private set; }

        internal Success(TFactoryFunction factoryFunction)
            : base(string.Empty)
        {
            FactoryFunction = factoryFunction;
        }
    }

    public record Failure : CliFactoryFunctionResult<TFactoryFunction>
    {
        public IEnumerable<string> Errors { get; private set; }

        internal Failure(IEnumerable<string> errors, string helpText)
            : base(helpText)
        {
            Errors = errors;
        }
    }

    public record Usage : CliFactoryFunctionResult<TFactoryFunction>
    {
        internal Usage(string helpText)
            : base(helpText)
        {
        }
    }
}


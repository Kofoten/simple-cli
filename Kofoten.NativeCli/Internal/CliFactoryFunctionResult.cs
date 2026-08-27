using System.Collections.Generic;
using System.ComponentModel;

namespace Kofoten.NativeCli.Internal;

/// <summary>
/// Represents the result of a factory function resolution that creates a CLI command or parser.
/// This type acts as a discriminated union and is designed to be evaluated using C# pattern matching.
/// </summary>
/// <remarks>
/// While this type is public to allow pattern matching via switch expressions, its constructors are internal. 
/// It is not intended to be extended or instantiated directly outside of the Kofoten.NativeCli library.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
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


using System;
using System.Collections.Generic;

namespace Kofoten.NativeCli;

public sealed class CliParseException(IEnumerable<string> errors, string helpText) : Exception(BuildMessage(errors))
{
    public IEnumerable<string> Errors { get; private set; } = errors;
    public string HelpText { get; private set; } = helpText;

    private static string BuildMessage(IEnumerable<string> errors)
    {
        var formattedErrors = string.Join(Environment.NewLine, errors);
        return $"Command failed with errors:{Environment.NewLine}{formattedErrors}";
    }
}

using System.Text.RegularExpressions;

namespace Kofoten.SimpleCli.Test;

public static class CliParsers
{
    public static bool TryParseLimit(string s, out int limit, out string? error)
    {
        if (int.TryParse(s, out limit))
        {
            if (limit > 0 && limit <= 100)
            {
                error = null;
                return true;
            }

            error = $"The value {s} must be greater than 0 and less than or equal to 100.";
            return false;
        }

        error = $"The value {s} can not pe parsed to an integer.";
        return false;
    }

    public static bool TryParseSignature(string s, out string? signature, out string? error)
    {
        var regex = new Regex("^[A-Z][a-z]*$");
        if (regex.IsMatch(s))
        {
            error = null;
            signature = s;
            return true;
        }

        error = $"Signature must contai9n only the characters a-z and be all lowercase except for the first wich must be an upper case character.";
        signature = null;
        return true;
    }
}

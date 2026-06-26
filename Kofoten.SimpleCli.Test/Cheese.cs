using System.Diagnostics.CodeAnalysis;

namespace Kofoten.SimpleCli.Test;

public record Cheese(string Name, string Origin)
{
    public static bool TryParse(string s, [NotNullWhen(true)] out Cheese? cheese, [NotNullWhen(false)] out string? error)
    {
        var delimiterIndex = s.IndexOf('|');
        if (delimiterIndex == -1)
        {
            cheese = null;
            error = "Invalid format: expected 'Name|Origin'";
            return false;
        }

        cheese = new Cheese(s[..delimiterIndex], s[(delimiterIndex + 1)..]);
        error = null;
        return true;
    }
}

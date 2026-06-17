using System.Diagnostics.CodeAnalysis;

namespace Kofoten.SimpleCli.Test;

public record Cheese(string Name, string Origin)
{
    public static bool TryParse(string s, [NotNullWhen(true)] out Cheese? cheese)
    {
        var delimiterIndex = s.IndexOf('|');
        if (delimiterIndex == -1)
        {
            cheese = null;
            return false;
        }

        cheese = new Cheese(s[..delimiterIndex], s[(delimiterIndex + 1)..]);
        return true;
    }
}

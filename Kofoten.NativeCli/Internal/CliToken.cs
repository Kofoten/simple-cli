using System;

namespace Kofoten.NativeCli.Internal;

public readonly ref struct CliToken(CliTokenType type, int index, int tokenStart, int tokenEnd)
{
    public CliTokenType Type { get; } = type;
    public int Index { get; } = index;
    public int TokenStart { get; } = tokenStart;
    public int TokenEnd { get; } = tokenEnd;

    public string GetTokenString(ArraySegment<string> args)
    {
        return args.Array[Index].Substring(TokenStart, TokenEnd - TokenStart);
    }
}

using System;

namespace Kofoten.NativeCli.Internal;

public readonly struct CliToken(CliTokenType type, int index, int tokenStart, int tokenLength)
{
    public CliTokenType Type { get; } = type;
    public int Index { get; } = index;
    public int TokenStart { get; } = tokenStart;
    public int TokenLength { get; } = tokenLength;

    public string GetTokenString(ArraySegment<string> args)
    {
        return args.Array[Index].Substring(TokenStart, TokenLength);
    }
}

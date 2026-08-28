namespace Kofoten.NativeCli.Internal;

public enum CliTokenType
{
    Unknown = 0,
    Value,
    Option,
    KnownOption,
    EndOfOptions,
}

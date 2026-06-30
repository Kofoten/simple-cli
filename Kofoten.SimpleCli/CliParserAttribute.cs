using System;

namespace Kofoten.SimpleCli;

/// <summary>
/// Marks a property with a custom parsing method. For use when implementing TryParse is not an option.
/// </summary>
/// <param name="type">The type that contains the parser method.</param>
/// <param name="methodName">The name of the parser method.</param>
/// <remarks>
/// If you require custom parsing for the key in a key value pair option use <see cref="CliKeyParserAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CliParserAttribute(Type type, string methodName) : Attribute
{
    /// <summary>
    /// The type that contains the parser method.
    /// </summary>
    public Type Type { get; } = type;

    /// <summary>
    /// The name of the parser method.
    /// </summary>
    public string MethodName { get; } = methodName;
}

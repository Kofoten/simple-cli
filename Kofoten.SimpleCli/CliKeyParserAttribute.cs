using System;

namespace Kofoten.SimpleCli;

/// <summary>
/// Marks a property with a custom parsing method for the kay of a key value pair option. For the value
/// use the regular <see cref="CliParserAttribute"/>. These are for use when implementing TryParse is
/// not an option.
/// </summary>
/// <param name="type">The type that contains the parser method.</param>
/// <param name="methodName">The name of the parser method.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CliKeyParserAttribute(Type type, string methodName) : Attribute
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

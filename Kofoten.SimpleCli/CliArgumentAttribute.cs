using System;

namespace Kofoten.SimpleCli;

/// <summary>
/// Marks a property as a positional CLI argument.
/// </summary>
/// <param name="position">The 0-based position of the argument.</param>
/// <param name="name">The display name for the argument (used in help text).</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CliArgumentAttribute(int position, string name) : Attribute
{
    /// <summary>
    /// The 0-based position of the argument.
    /// </summary>
    public int Position { get; } = position;

    /// <summary>
    /// The display name for the argument (used in help text).
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The description of the argument (used in help text).
    /// </summary>
    public string? Description { get; set; }
}

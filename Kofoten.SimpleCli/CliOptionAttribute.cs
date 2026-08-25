using System;

namespace Kofoten.SimpleCli;

/// <summary>
/// Marks a property as a CLI option.
/// </summary>
/// <param name="name">The name of the option (double dashes are automatically added before).</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CliOptionAttribute(string name) : Attribute
{
    /// <summary>
    /// The name of the option (double dashes are automatically added before).
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The shorthand for the option (single dash is automatically added before).
    /// Defaults to '\0' if not set, which indicates there is no shorthand for this option.
    /// </summary>
    public char Short { get; set; } = '\0';

    /// <summary>
    /// The description of the option (used in help text).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates if this option should be hidden from the help page.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// The implicit value of the option (used when the option is provided without a value).
    /// </summary>
    public string? ImplicitValue { get; set; }
}

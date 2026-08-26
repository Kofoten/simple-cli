using Microsoft.CodeAnalysis;

namespace Kofoten.NativeCli.Generator.Diagnostics
{
    internal class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor InvalidPublicConstructorCount = new(
            id: "NCLI001",
            title: "Command must have exactly one public constructor",
            messageFormat: "Type '{0}' must declare exactly one public constructor to be CLI-parsable",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedCollectionElementType = new(
            id: "NCLI002",
            title: "Unable to resolve collection element type",
            messageFormat: "Property '{0}' has a collection type whose element type could not be resolved",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateArgumentPosition = new(
            id: "NCLI003",
            title: "Duplicate CLI argument position",
            messageFormat: "Property '{0}' uses argument position '{1}', which is already used in command '{2}'",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateOptionName = new(
            id: "NCLI004",
            title: "Duplicate CLI option name",
            messageFormat: "Property '{0}' uses option name '--{1}', which is already used in command '{2}'",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateOptionShortName = new(
            id: "NCLI005",
            title: "Duplicate CLI short option",
            messageFormat: "Property '{0}' uses short option '-{1}', which is already used in command '{2}'",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ReservedHelpOption = new(
            id: "NCLI006",
            title: "Reserved CLI option",
            messageFormat: "Property '{0}' uses a reserved help option ('{1}'). '-h' and '--help' are reserved by the native-cli router.",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedCollectionType = new(
            id: "NCLI007",
            title: "Unsupported collection type",
            messageFormat: "The type '{0}' of property '{1}' is not a supportd collection type. See https://github.com/Kofoten/native-cli/blob/main/README.md#supported-property-types for more information.",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor AmbiguousCliPropertyBinding = new(
            id: "NCLI008",
            title: "Ambiguous CLI property binding",
            messageFormat: "Ambiguous CLI binding of property '{0}'. A CLI property may not be both an argument and an option.",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingParser = new(
            id: "NCLI009",
            title: "Missing parser",
            messageFormat: "The type '{0}' of property '{1}' does not have any valid parser",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidCommandAccessibility = new(
            id: "NCLI010",
            title: "Invalid command accessibility",
            messageFormat: "Command must be declared as public or internal",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor RequiredPropertyWithDefaultValue = new(
            id: "NCLI011",
            title: "Required property with default value",
            messageFormat: "A required argument or option property should not have a default value",
            category: "Kofoten.NativeCli.Generator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}

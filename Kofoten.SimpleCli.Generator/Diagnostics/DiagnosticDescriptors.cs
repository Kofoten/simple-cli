using Microsoft.CodeAnalysis;

namespace Kofoten.SimpleCli.Generator.Diagnostics
{
    internal class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor InvalidPublicConstructorCount = new(
            id: "SCLI001",
            title: "Command must have exactly one public constructor",
            messageFormat: "Type '{0}' must declare exactly one public constructor to be CLI-parsable",
            category: "Kofoten.SimpleCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedCollectionElementType = new(
            id: "SCLI002",
            title: "Unable to resolve collection element type",
            messageFormat: "Property '{0}' has a collection type whose element type could not be resolved",
            category: "Kofoten.SimpleCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateArgumentPosition = new(
            id: "SCLI003",
            title: "Duplicate CLI argument position",
            messageFormat: "Property '{0}' uses argument position '{1}', which is already used in command '{2}'",
            category: "Kofoten.SimpleCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateOptionName = new(
            id: "SCLI004",
            title: "Duplicate CLI option name",
            messageFormat: "Property '{0}' uses option name '--{1}', which is already used in command '{2}'",
            category: "Kofoten.SimpleCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateOptionShortName = new(
            id: "SCLI005",
            title: "Duplicate CLI short option",
            messageFormat: "Property '{0}' uses short option '-{1}', which is already used in command '{2}'",
            category: "Kofoten.SimpleCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ReservedHelpOption = new(
            id: "SCLI006",
            title: "Reserved CLI option",
            messageFormat: "Property '{0}' uses a reserved help option ('{1}'). '-h' and '--help' are reserved by the simple-cli router.",
            category: "Kofoten.SimpleCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedCollectionType = new(
            id: "SCLI007",
            title: "Unsupported collection type",
            messageFormat: "The type '{0}' of property '{1}' is not a supportd collection type. See https://github.com/Kofoten/simple-cli/blob/main/README.md#supported-property-types for more information.",
            category: "Kofoten.SimpleCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor AmbiguousCliPropertyBinding = new(
            id: "SCLI008",
            title: "Ambiguous CLI property binding",
            messageFormat: "Ambiguous CLI binding of property '{0}'. A CLI property may not be both an argument and an option.",
            category: "Kofoten.SimpleCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingParser = new(
            id: "SCLI009",
            title: "Missing parser",
            messageFormat: "The type '{0}' of property '{1}' does not have any valid parser",
            category: "Kofoten.SimpleCli.Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}

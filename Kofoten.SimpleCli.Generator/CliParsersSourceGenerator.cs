using Kofoten.SimpleCli.Generator.Data;
using Kofoten.SimpleCli.Generator.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;

namespace Kofoten.SimpleCli.Generator;

[Generator]
public class CliParsersSourceGenerator : IIncrementalGenerator
{
    public const string DefaultParserMethodName = "TryParse";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c && c.BaseList is not null,
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node);

        var simpleCliCompilationContextProvider = context.CompilationProvider.Select(static (compilation, _) =>
        {
            var serviceProviderSymbol = compilation.GetTypeByMetadataName("System.IServiceProvider");
            var getRequiredServiceExtensionsSymbol = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");

            var hasDependencyInjection =
                serviceProviderSymbol is not null
                &&
                getRequiredServiceExtensionsSymbol is not null;

            var supportedEnumerableOfTInterfaces = new List<INamedTypeSymbol>(5);

            var iEnumerableOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
            if (iEnumerableOfT != null)
            {
                supportedEnumerableOfTInterfaces.Add(iEnumerableOfT);
            }

            var iCollectionOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.ICollection`1");
            if (iCollectionOfT != null)
            {
                supportedEnumerableOfTInterfaces.Add(iCollectionOfT);
            }

            var iReadOnlyCollectionOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyCollection`1");
            if (iReadOnlyCollectionOfT != null)
            {
                supportedEnumerableOfTInterfaces.Add(iReadOnlyCollectionOfT);
            }

            var iListOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.IList`1");
            if (iListOfT != null)
            {
                supportedEnumerableOfTInterfaces.Add(iListOfT);
            }

            var iReadOnlyListOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1");
            if (iReadOnlyListOfT != null)
            {
                supportedEnumerableOfTInterfaces.Add(iReadOnlyListOfT);
            }

            var supportedDictionaryOfKVInterfaces = new List<INamedTypeSymbol>();

            var iDictionaryOfKV = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2");
            if (iDictionaryOfKV != null)
            {
                supportedDictionaryOfKVInterfaces.Add(iDictionaryOfKV);
            }

            var iReadOnlyDictionaryOfKV = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyDictionary`2");
            if (iReadOnlyDictionaryOfKV != null)
            {
                supportedDictionaryOfKVInterfaces.Add(iReadOnlyDictionaryOfKV);
            }

            return new SimpleCliCompilationContext(
                CliParsableSymbol: compilation.GetTypeByMetadataName("Kofoten.SimpleCli.ICliParsable"),
                CliValidationResultSymbol: compilation.GetTypeByMetadataName("Kofoten.SimpleCli.CliValidationResult"),
                CliArgumentAttributeSymbol: compilation.GetTypeByMetadataName("Kofoten.SimpleCli.CliArgumentAttribute"),
                CliOptionAttributeSymbol: compilation.GetTypeByMetadataName("Kofoten.SimpleCli.CliOptionAttribute"),
                CliParserAttributeSymbol: compilation.GetTypeByMetadataName("Kofoten.SimpleCli.CliParserAttribute"),
                CliKeyParserAttributeSymbol: compilation.GetTypeByMetadataName("Kofoten.SimpleCli.CliKeyParserAttribute"),
                CliFlagsAttributeSymbol: compilation.GetTypeByMetadataName("System.FlagsAttribute"),
                EnumerableOfTSymbol: iEnumerableOfT,
                KeyValuePairOfT2Symbol: compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2"),
                ListOfTSymbol: compilation.GetTypeByMetadataName("System.Collections.Generic.List`1"),
                DictionaryOfKVSymbol: compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2"),
                ImmutableArrayOfTSymbol: compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray`1"),
                ImmutableListOfTSymbol: compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList`1"),
                ImmutableHashSetOfTSymbol: compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableHashSet`1"),
                ImmutableDictionaryOfKVSymbol: compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary`2"),
                FrozenSetOfTSymbol: compilation.GetTypeByMetadataName("System.Collections.Frozen.FrozenSet`1"),
                FrozenDictionaryOfKVSymbol: compilation.GetTypeByMetadataName("System.Collections.Frozen.FrozenDictionary`2"),
                SupportedEnumerableOfTInterfaceSymbols: supportedEnumerableOfTInterfaces,
                SupportedDictionaryOfKVInterfaceSymbols: supportedDictionaryOfKVInterfaces,
                HasDependencyInjection: hasDependencyInjection);
        });

        var combinedProvider = classDeclarations
            .Combine(context.CompilationProvider)
            .Combine(simpleCliCompilationContextProvider);

        var commandModels = combinedProvider.Select(static (source, _) =>
        {
            var classDecl = source.Left.Left;
            var compilation = source.Left.Right;
            var simpleCliContext = source.Right;

            var semanticModel = compilation.GetSemanticModel(classDecl.SyntaxTree);

            return GetCommandTarget(classDecl, semanticModel, simpleCliContext);
        });

        context.RegisterSourceOutput(commandModels, static (spc, result) =>
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }

            if (result.Command is not null)
            {
                GenerateParser(spc, result.Command);
            }
        });
    }

    #region BuildCommandModel

    private static CommandGenerationResult GetCommandTarget(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, SimpleCliCompilationContext simpleCliContext)
    {
        //#if DEBUG
        //        if (!global::System.Diagnostics.Debugger.IsAttached)
        //        {
        //            global::System.Diagnostics.Debugger.Launch();
        //        }
        //#endif

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var compilationUnit = (CompilationUnitSyntax)classDecl.SyntaxTree.GetRoot();
        var usings = compilationUnit.Usings.Select(x => x.ToString()).ToList();

        if (semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
        {
            return new CommandGenerationResult(null, diagnostics.ToImmutable());
        }

        if (classSymbol.IsAbstract)
        {
            return new CommandGenerationResult(null, diagnostics.ToImmutable());
        }

        var compilation = semanticModel.Compilation;

        var inheritsCommand = classSymbol.AllInterfaces.Any(interfaceSymbol =>
            SymbolEqualityComparer.Default.Equals(interfaceSymbol, simpleCliContext.CliParsableSymbol));

        if (!inheritsCommand)
        {
            return new CommandGenerationResult(null, diagnostics.ToImmutable());
        }

        var accessibility = "internal";
        if (classSymbol.DeclaredAccessibility == Accessibility.Public
            ||
            classSymbol.DeclaredAccessibility == Accessibility.Internal)
        {
            accessibility = classSymbol.DeclaredAccessibility.ToString().ToLower();
        }
        else
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.InvalidCommandAccessibility,
                classDecl.Identifier.GetLocation()));
        }

        var publicConstructors = classSymbol.Constructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        if (publicConstructors.Count != 1)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.InvalidPublicConstructorCount,
                classDecl.Identifier.GetLocation(),
                classSymbol.Name));

            return new CommandGenerationResult(null, diagnostics.ToImmutable());
        }

        var constructorParams = new List<ConstructorParameterModel>();
        foreach (var param in publicConstructors[0].Parameters)
        {
            constructorParams.Add(new ConstructorParameterModel(
                Name: param.Name,
                TypeName: param.Type.ToDisplayString()
            ));
        }

        var hasValidationMethod = false;
        var properties = new List<PropertyModel>();
        var processedPropertyNames = new HashSet<string>();
        var propertySymbolsByName = new Dictionary<string, IPropertySymbol>();
        var currentClassSymbol = classSymbol;
        while (currentClassSymbol is not null && currentClassSymbol.SpecialType != SpecialType.System_Object)
        {
            if (!hasValidationMethod)
            {
                hasValidationMethod = currentClassSymbol.GetMembers().OfType<IMethodSymbol>()
                    .Where(m => m.Name == "Validate")
                    .Where(m => !m.IsAbstract && !m.IsStatic && !m.IsAsync)
                    .Where(m => m.Parameters.Length == 0)
                    .Where(m => m.DeclaredAccessibility == Accessibility.Public || m.DeclaredAccessibility == Accessibility.Internal)
                    .Any(m => SymbolEqualityComparer.Default.Equals(m.ReturnType, simpleCliContext.CliValidationResultSymbol));
            }

            foreach (var member in currentClassSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                if (!processedPropertyNames.Add(member.Name))
                {
                    // NOTE: Skip properties that have been overridden in a subclass.
                    continue;
                }

                propertySymbolsByName[member.Name] = member;

                var argAttribute = member.GetAttributes().FirstOrDefault(a =>
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, simpleCliContext.CliArgumentAttributeSymbol));

                var optAttribute = member.GetAttributes().FirstOrDefault(a =>
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, simpleCliContext.CliOptionAttributeSymbol));

                if (argAttribute == null && optAttribute == null)
                {
                    // NOTE: Property is not decorated as a CLI option or argument and should therfore be skipped.
                    continue;
                }

                if (argAttribute != null && optAttribute != null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.AmbiguousCliPropertyBinding,
                        member.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation(),
                        member.Name));

                    continue;
                }

                var parserAttribute = member.GetAttributes().FirstOrDefault(a =>
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, simpleCliContext.CliParserAttributeSymbol));

                var keyParserAttribute = member.GetAttributes().FirstOrDefault(a =>
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, simpleCliContext.CliKeyParserAttributeSymbol));

                string typeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                ITypeSymbol valueTypeSymbol = member.Type;
                ITypeSymbol? keyTypeSymbol = null;
                bool isString = valueTypeSymbol.SpecialType == SpecialType.System_String;
                bool isEnum = valueTypeSymbol.TypeKind == TypeKind.Enum;
                bool isFlagsEnum = false;
                bool isCollection = false;
                CollectionType collectionType = CollectionType.None;
                bool isDictionary = false;

                if (!isString && TryGetEnumerableElementType(member.Type, simpleCliContext, out var elementType))
                {
                    if (elementType is null)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            DiagnosticDescriptors.UnsupportedCollectionElementType,
                            member.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation(),
                            member.Name));

                        continue;
                    }
                    else if (TryGetKeyValueTypeArgs(elementType, simpleCliContext, out keyTypeSymbol, out var foundValueType))
                    {
                        if (TryGetCollectionType(member.Type, keyTypeSymbol!, foundValueType!, simpleCliContext, out collectionType))
                        {
                            valueTypeSymbol = foundValueType!;
                            isDictionary = true;
                        }
                        else
                        {
                            diagnostics.Add(Diagnostic.Create(
                                DiagnosticDescriptors.UnsupportedCollectionType,
                                member.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation(),
                                typeName,
                                member.Name));

                            continue;
                        }
                    }
                    else
                    {
                        if (TryGetCollectionType(member.Type, elementType, simpleCliContext, out collectionType))
                        {
                            valueTypeSymbol = elementType;
                            isCollection = true;
                        }
                        else
                        {
                            diagnostics.Add(Diagnostic.Create(
                                DiagnosticDescriptors.UnsupportedCollectionType,
                                member.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation(),
                                typeName,
                                member.Name));

                            continue;
                        }
                    }
                }

                string valueTypeName = valueTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string? keyTypeName = keyTypeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string valueParserMethodName = string.Empty;
                string? keyParserMethodName = string.Empty;
                bool valueHasValidParser = false;
                bool keyHasValidParser = false;
                bool valueHasErrorMessageOut = false;
                bool keyHasErrorMessageOut = false;

                if (parserAttribute != null)
                {
                    if (parserAttribute.ConstructorArguments.Length == 2
                        &&
                        parserAttribute.ConstructorArguments[0].Value is INamedTypeSymbol customParserTypeSymbol
                        &&
                        parserAttribute.ConstructorArguments[1].Value is string customParserMethodName)
                    {
                        string customParserTypeName = customParserTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        (valueHasValidParser, valueHasErrorMessageOut) = InspectParserSignature(customParserTypeSymbol, customParserMethodName);
                        valueParserMethodName = $"{customParserTypeName}.{customParserMethodName}";
                    }
                }
                else if (isEnum)
                {
                    valueHasValidParser = true;
                    isFlagsEnum = valueTypeSymbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, simpleCliContext.CliFlagsAttributeSymbol));
                }
                else if (valueTypeSymbol.SpecialType == SpecialType.System_String) // NOTE: valueTypeSymbol may have changed after first isString check.
                {
                    valueHasValidParser = true;
                    valueHasErrorMessageOut = false;
                }
                else
                {
                    (valueHasValidParser, valueHasErrorMessageOut) = InspectParserSignature(valueTypeSymbol, DefaultParserMethodName);
                    valueParserMethodName = $"{valueTypeName}.{DefaultParserMethodName}";
                }

                if (!isEnum && !isString && isDictionary)
                {
                    if (keyParserAttribute != null)
                    {
                        if (keyParserAttribute.ConstructorArguments.Length == 2
                            &&
                            keyParserAttribute.ConstructorArguments[0].Value is INamedTypeSymbol customParserTypeSymbol
                            &&
                            keyParserAttribute.ConstructorArguments[1].Value is string customParserMethodName)
                        {
                            string customParserTypeName = customParserTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                            (keyHasValidParser, keyHasErrorMessageOut) = InspectParserSignature(customParserTypeSymbol, customParserMethodName);
                            keyParserMethodName = $"{customParserTypeName}.{customParserMethodName}";
                        }
                    }
                    else if (keyTypeSymbol!.SpecialType == SpecialType.System_String)
                    {
                        keyHasValidParser = true;
                        keyHasErrorMessageOut = false;
                    }
                    else
                    {
                        (keyHasValidParser, keyHasErrorMessageOut) = InspectParserSignature(keyTypeSymbol!, DefaultParserMethodName);
                        keyParserMethodName = $"{keyTypeName}.{DefaultParserMethodName}";
                    }
                }

                if (!valueHasValidParser)
                {
                    diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.MissingParser,
                        member.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation(),
                        valueTypeName,
                        member.Name));

                    continue;
                }

                if (!keyHasValidParser && isDictionary)
                {
                    diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.MissingParser,
                        member.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation(),
                        keyTypeName,
                        member.Name));

                    continue;
                }

                string? defaultValueSyntax = null;
                string? defaultValueString = null;
                var syntaxReference = member.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxReference != null)
                {
                    var syntaxNode = syntaxReference.GetSyntax();
                    if (syntaxNode is PropertyDeclarationSyntax propertySyntax
                        &&
                        propertySyntax.Initializer != null)
                    {
                        defaultValueSyntax = propertySyntax.Initializer.Value.ToString();

                        var propertySemanticModel = compilation.GetSemanticModel(propertySyntax.SyntaxTree);
                        var constantValue = propertySemanticModel.GetConstantValue(propertySyntax.Initializer.Value);
                        if (constantValue.HasValue)
                        {
                            if (isEnum || isFlagsEnum)
                            {
                                defaultValueString = GetEnumDefaultValue(valueTypeSymbol, constantValue.Value!, isFlagsEnum);
                            }
                            else
                            {
                                defaultValueString = constantValue.Value?.ToString();
                            }
                        }
                        else if (isCollection || isDictionary)
                        {
                            defaultValueString = FormatCollectionDefault(defaultValueSyntax, isDictionary);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(defaultValueSyntax))
                            {
                                defaultValueString = defaultValueSyntax;
                            }
                        }
                    }
                }

                if (member.IsRequired && defaultValueString is not null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.RequiredPropertyWithDefaultValue,
                        member.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation()));
                }

                string[] allowedValueStrings = [];
                if (valueTypeSymbol.TypeKind == TypeKind.Enum)
                {
                    allowedValueStrings = [.. valueTypeSymbol
                        .GetMembers()
                        .OfType<IFieldSymbol>()
                        .Where(f => f.HasConstantValue)
                        .Select(f => f.Name)];
                }

                if (argAttribute != null
                    &&
                    argAttribute.ConstructorArguments.Length > 0
                    &&
                    argAttribute.ConstructorArguments[0].Value is int position)
                {
                    var descriptionArg = argAttribute.NamedArguments.FirstOrDefault(na => na.Key == "Description");
                    var description = descriptionArg.Value.Value is string d ? d : string.Empty;

                    properties.Add(new ArgumentPropertyModel(
                        Name: member.Name,
                        TypeName: typeName,
                        ValueTypeName: valueTypeName,
                        SpecialType: valueTypeSymbol.SpecialType,
                        KeySpecialType: keyTypeSymbol?.SpecialType ?? SpecialType.None,
                        ValueSpecialType: valueTypeSymbol.SpecialType,
                        IsRequired: member.IsRequired,
                        Description: description,
                        ValueParseMethodName: valueParserMethodName,
                        ValueHasErrorMessageOut: valueHasErrorMessageOut,
                        Position: position,
                        IsEnum: isEnum));
                }
                else if (optAttribute != null
                    &&
                    optAttribute.ConstructorArguments.Length > 0
                    &&
                    optAttribute.ConstructorArguments[0].Value is string optName)
                {
                    var shortArg = optAttribute.NamedArguments.FirstOrDefault(na => na.Key == "Short");
                    char? shortName = shortArg.Value.Value is char c && c != '\0' ? c : null;

                    if (string.Equals(optName, "help", StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            DiagnosticDescriptors.ReservedHelpOption,
                            member.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation(),
                            member.Name,
                            "--help"));
                    }

                    if (shortName == 'h')
                    {
                        diagnostics.Add(Diagnostic.Create(
                            DiagnosticDescriptors.ReservedHelpOption,
                            member.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation(),
                            member.Name,
                            "-h"));
                    }

                    var descriptionArg = optAttribute.NamedArguments.FirstOrDefault(x => x.Key == "Description");
                    var description = descriptionArg.Value.Value is string d ? d : string.Empty;

                    var hiddenArg = optAttribute.NamedArguments.FirstOrDefault(x => x.Key == "Hidden");
                    var hidden = hiddenArg.Value.Value is bool h && h;

                    var implicitValueArg = optAttribute.NamedArguments.FirstOrDefault(x => x.Key == "ImplicitValue");
                    var implicitValue = implicitValueArg.Value.Value is string iv ? iv : null;

                    if (implicitValue is not null && (isCollection || isDictionary))
                    {
                        // TODO: Emit diagnostic for implicit value assignment not supported for this property type.
                    }

                    properties.Add(new OptionPropertyModel(
                        Name: member.Name,
                        TypeName: typeName,
                        ValueTypeName: valueTypeName,
                        KeyTypeName: keyTypeName,
                        SpecialType: member.Type.SpecialType,
                        KeySpecialType: keyTypeSymbol?.SpecialType ?? SpecialType.None,
                        ValueSpecialType: valueTypeSymbol.SpecialType,
                        IsRequired: member.IsRequired,
                        Description: description,
                        ValueParseMethodName: valueParserMethodName,
                        ValueHasErrorMessageOut: valueHasErrorMessageOut,
                        KeyParseMethodName: keyParserMethodName,
                        KeyHasErrorMessageOut: keyHasErrorMessageOut,
                        DefaultValueString: defaultValueString,
                        DefaultValueSyntax: defaultValueSyntax,
                        AllowedValueStrings: allowedValueStrings,
                        OptionName: optName,
                        ShortName: shortName,
                        IsCollection: isCollection,
                        CollectionType: collectionType,
                        IsDictionary: isDictionary,
                        IsEnum: isEnum,
                        IsFlagsEnum: isFlagsEnum,
                        Hidden: hidden,
                        ImplicitValueString: implicitValue));
                }
            }

            currentClassSymbol = currentClassSymbol.BaseType;
        }

        Location GetLocation(string propertyName) =>
            propertySymbolsByName.TryGetValue(propertyName, out var p)
                ? (p.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation())
                : classDecl.Identifier.GetLocation();

        foreach (var g in properties.OfType<ArgumentPropertyModel>().GroupBy(a => a.Position).Where(g => g.Count() > 1))
        {
            foreach (var p in g)
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateArgumentPosition,
                    GetLocation(p.Name),
                    p.Name,
                    g.Key,
                    classSymbol.Name));
            }
        }

        foreach (var g in properties
            .OfType<OptionPropertyModel>()
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionName))
            .GroupBy(o => o.OptionName, System.StringComparer.Ordinal)
            .Where(g => g.Count() > 1))
        {
            foreach (var p in g)
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateOptionName,
                    GetLocation(p.Name),
                    p.Name,
                    p.OptionName,
                    classSymbol.Name));
            }
        }

        foreach (var g in properties
            .OfType<OptionPropertyModel>()
            .Where(o => o.ShortName.HasValue)
            .GroupBy(o => o.ShortName!.Value)
            .Where(g => g.Count() > 1))
        {
            foreach (var p in g)
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateOptionShortName,
                    GetLocation(p.Name),
                    p.Name,
                    p.ShortName!.Value,
                    classSymbol.Name));
            }
        }

        var command = new CommandModel(
            Namespace: classSymbol.ContainingNamespace.ToDisplayString(),
            Accessibility: accessibility,
            ClassName: classSymbol.Name,
            Description: GetCommandDescription(classSymbol),
            ConstructorParameters: constructorParams,
            Properties: properties,
            HasDependencyInjection: simpleCliContext.HasDependencyInjection,
            HasValidationMethod: hasValidationMethod,
            Usings: usings);

        return new CommandGenerationResult(command, diagnostics.ToImmutable());
    }

    private static bool TryGetEnumerableElementType(
        ITypeSymbol type,
        SimpleCliCompilationContext simpleCliContext,
        out ITypeSymbol? elementType)
    {
        elementType = null;

        if (simpleCliContext.EnumerableOfTSymbol is null)
        {
            return false;
        }

        if (type is INamedTypeSymbol named &&
            named.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, simpleCliContext.EnumerableOfTSymbol))
        {
            elementType = named.TypeArguments[0];
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (iface is INamedTypeSymbol i &&
                i.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, simpleCliContext.EnumerableOfTSymbol))
            {
                elementType = i.TypeArguments[0];
                return true;
            }
        }

        return false;
    }

    private static bool TryGetKeyValueTypeArgs(
        ITypeSymbol elementType,
        SimpleCliCompilationContext simpleCliContext,
        out ITypeSymbol? keyType,
        out ITypeSymbol? valueType)
    {
        keyType = null;
        valueType = null;

        if (simpleCliContext.KeyValuePairOfT2Symbol is null)
        {
            return false;
        }

        if (elementType is INamedTypeSymbol named &&
            named.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, simpleCliContext.KeyValuePairOfT2Symbol))
        {
            keyType = named.TypeArguments[0];
            valueType = named.TypeArguments[1];
            return true;
        }

        return false;
    }

    private static bool TryGetCollectionType(
        ITypeSymbol type,
        ITypeSymbol elementType,
        SimpleCliCompilationContext simpleCliContext,
        out CollectionType collectionType)
    {
        if (simpleCliContext.EnumerableOfTSymbol == null)
        {
            collectionType = CollectionType.None;
            return false;
        }

        if (type.TypeKind == TypeKind.Array)
        {
            collectionType = CollectionType.Array;
            return true;
        }

        if (type is INamedTypeSymbol named)
        {
            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.ListOfTSymbol?.Construct(elementType)))
            {
                collectionType = CollectionType.ListCompatible;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.ImmutableArrayOfTSymbol?.Construct(elementType)))
            {
                collectionType = CollectionType.ImmutableArray;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.ImmutableListOfTSymbol?.Construct(elementType)))
            {
                collectionType = CollectionType.ImmutableList;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.ImmutableHashSetOfTSymbol?.Construct(elementType)))
            {
                collectionType = CollectionType.ImmutableHashSet;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.FrozenSetOfTSymbol?.Construct(elementType)))
            {
                collectionType = CollectionType.FrozenSet;
                return true;
            }

            if (simpleCliContext.SupportedEnumerableOfTInterfaceSymbols.Any(s => SymbolEqualityComparer.Default.Equals(named, s.Construct(elementType))))
            {
                collectionType = CollectionType.ListCompatible;
                return true;
            }

            var typedEnumerableSymbol = simpleCliContext.EnumerableOfTSymbol.Construct(elementType);
            var constructor = named.InstanceConstructors.FirstOrDefault(c =>
                c.DeclaredAccessibility == Accessibility.Public
                &&
                c.Parameters.Length == 1
                &&
                c.Parameters[0].Type is INamedTypeSymbol paramType
                &&
                paramType.IsGenericType
                &&
                SymbolEqualityComparer.Default.Equals(paramType, typedEnumerableSymbol));

            if (constructor != null)
            {
                collectionType = CollectionType.ConstructorCompatible;
                return true;
            }
        }

        collectionType = CollectionType.None;
        return false;
    }

    private static string GetEnumDefaultValue(ITypeSymbol enumType, object constantValue, bool isFlagsEnum)
    {
        try
        {
            long value = Convert.ToInt64(constantValue);
            var fields = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => f.HasConstantValue)
                .ToList();

            if (value == 0)
            {
                var zeroField = fields.FirstOrDefault(f => Convert.ToInt64(f.ConstantValue) == 0);
                return zeroField?.Name ?? "0";
            }

            if (isFlagsEnum)
            {
                var names = new List<string>();
                foreach (var field in fields)
                {
                    long fieldValue = Convert.ToInt64(field.ConstantValue);
                    if (fieldValue != 0 && (value & fieldValue) == fieldValue)
                    {
                        names.Add(field.Name);
                    }
                }
                return names.Count > 0 ? string.Join(", ", names) : value.ToString();
            }

            var exactField = fields.FirstOrDefault(f => Convert.ToInt64(f.ConstantValue) == value);
            return exactField?.Name ?? value.ToString();
        }
        catch
        {
            return constantValue?.ToString() ?? string.Empty;
        }
    }

    private static string FormatCollectionDefault(string text, bool isDictionary)
    {
        text = text.Trim();
        var start = text.IndexOf('{');
        if (start == -1)
        {
            start = text.IndexOf('[');
        }

        var end = text.LastIndexOf('}');
        if (end == -1)
        {
            end = text.LastIndexOf(']');
        }

        if (start == -1 || end == -1 || end < start)
        {
            if (text.StartsWith("new") || text.Contains(".Empty"))
            {
                return "[]";
            }

            return text;
        }

        string content = text.Substring(start + 1, end - start - 1).Trim();
        if (string.IsNullOrEmpty(content))
        {
            return "[]";
        }

        var items = new List<string>();
        bool inString = false;
        bool escapeNext = false;
        int braceDepth = 0;
        int bracketDepth = 0;
        int parenDepth = 0;
        int lastSplit = 0;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                switch (c)
                {
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth--;
                        break;
                    case '[':
                        bracketDepth++;
                        break;
                    case ']':
                        bracketDepth--;
                        break;
                    case '(':
                        parenDepth++;
                        break;
                    case ')':
                        parenDepth--;
                        break;
                    case ',' when braceDepth == 0 && bracketDepth == 0 && parenDepth == 0:
                        items.Add(content.Substring(lastSplit, i - lastSplit));
                        lastSplit = i + 1;
                        break;
                    default:
                        break;
                }
            }
        }

        var finalItem = content.Substring(lastSplit, content.Length - lastSplit);
        if (!string.IsNullOrWhiteSpace(finalItem))
        {
            items.Add(finalItem);
        }

        var formattedItems = new List<string>();
        foreach (var item in items)
        {
            string cleaned = item.Trim();

            if (isDictionary)
            {
                cleaned = CleanKeyValuePair(cleaned);
            }
            else
            {
                cleaned = cleaned.Trim('"', '\'');
            }

            formattedItems.Add(cleaned);
        }

        return "[" + string.Join(" ", formattedItems) + "]";
    }

    private static string CleanKeyValuePair(string pair)
    {
        pair = pair.Trim();
        string key = string.Empty;
        string value = string.Empty;

        if (pair.StartsWith("["))
        {
            int closeBracket = pair.IndexOf(']');
            int equals = pair.IndexOf('=', closeBracket);

            if (closeBracket != -1 && equals != -1)
            {
                key = pair.Substring(1, closeBracket - 1).Trim();
                value = pair.Substring(equals + 1).Trim();
            }
        }
        else if (pair.StartsWith("{") && pair.EndsWith("}"))
        {
            string inner = pair.Substring(1, pair.Length - 2).Trim();
            int firstComma = inner.IndexOf(',');
            if (firstComma != -1)
            {
                key = inner.Substring(0, firstComma).Trim();
                value = inner.Substring(firstComma + 1).Trim();
            }
        }
        else
        {
            return pair;
        }

        key = key.Trim('"', '\'');
        value = value.Trim('"', '\'');

        return $"{key}={value}";
    }

    /// <summary>
    /// A version of TryGetCollectionType that looks for collections of <see cref="KeyValuePair{TKey, TValue}"/> items including dictionary collections.
    /// </summary>
    private static bool TryGetCollectionType(
        ITypeSymbol type,
        ITypeSymbol keyType,
        ITypeSymbol valueType,
        SimpleCliCompilationContext simpleCliContext,
        out CollectionType collectionType)
    {
        if (simpleCliContext.KeyValuePairOfT2Symbol == null
            ||
            simpleCliContext.EnumerableOfTSymbol == null)
        {
            collectionType = CollectionType.None;
            return false;
        }

        if (type.TypeKind == TypeKind.Array)
        {
            collectionType = CollectionType.Array;
            return true;
        }

        var keyValuePairSymbol = simpleCliContext.KeyValuePairOfT2Symbol.Construct(keyType, valueType);
        if (type is INamedTypeSymbol named)
        {
            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.DictionaryOfKVSymbol?.Construct(keyType, valueType)))
            {
                collectionType = CollectionType.DictionaryCompatible;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.ImmutableDictionaryOfKVSymbol?.Construct(keyType, valueType)))
            {
                collectionType = CollectionType.ImmutableDictionary;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.FrozenDictionaryOfKVSymbol?.Construct(keyType, valueType)))
            {
                collectionType = CollectionType.FrozenDictionary;
                return true;
            }

            if (simpleCliContext.SupportedDictionaryOfKVInterfaceSymbols.Any(s => SymbolEqualityComparer.Default.Equals(named, s.Construct(keyType, valueType))))
            {
                collectionType = CollectionType.DictionaryCompatible;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.ListOfTSymbol?.Construct(keyValuePairSymbol)))
            {
                collectionType = CollectionType.ListCompatible;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.ImmutableArrayOfTSymbol?.Construct(keyValuePairSymbol)))
            {
                collectionType = CollectionType.ImmutableArray;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.ImmutableListOfTSymbol?.Construct(keyValuePairSymbol)))
            {
                collectionType = CollectionType.ImmutableList;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.ImmutableHashSetOfTSymbol?.Construct(keyValuePairSymbol)))
            {
                collectionType = CollectionType.ImmutableHashSet;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(named, simpleCliContext.FrozenSetOfTSymbol?.Construct(keyValuePairSymbol)))
            {
                collectionType = CollectionType.FrozenSet;
                return true;
            }

            if (simpleCliContext.SupportedEnumerableOfTInterfaceSymbols.Any(s => SymbolEqualityComparer.Default.Equals(named, s.Construct(keyValuePairSymbol))))
            {
                collectionType = CollectionType.ListCompatible;
                return true;
            }

            var typedEnumerableSymbol = simpleCliContext.EnumerableOfTSymbol.Construct(keyValuePairSymbol);
            var constructor = named.InstanceConstructors.FirstOrDefault(c =>
                c.DeclaredAccessibility == Accessibility.Public
                &&
                c.Parameters.Length == 1
                &&
                c.Parameters[0].Type is INamedTypeSymbol paramType
                &&
                paramType.IsGenericType
                &&
                SymbolEqualityComparer.Default.Equals(paramType, typedEnumerableSymbol));

            if (constructor != null)
            {
                collectionType = CollectionType.ConstructorCompatible;
                return true;
            }
        }

        collectionType = CollectionType.None;
        return false;
    }

    private static (bool IsValid, bool HasErrorMessageOut) InspectParserSignature(ITypeSymbol targetType, string methodName)
    {
        var methods = targetType.GetMembers(methodName).OfType<IMethodSymbol>();
        foreach (var method in methods)
        {
            if (!method.IsStatic || method.ReturnType.SpecialType != SpecialType.System_Boolean)
            {
                continue;
            }

            var parameters = method.Parameters;
            if (parameters.Length == 2
                &&
                parameters[0].Type.SpecialType == SpecialType.System_String
                &&
                parameters[1].RefKind == RefKind.Out)
            {
                return (true, false);
            }

            if (parameters.Length == 3
                &&
                parameters[0].Type.SpecialType == SpecialType.System_String
                &&
                parameters[1].RefKind == RefKind.Out
                &&
                parameters[2].Type.SpecialType == SpecialType.System_String
                &&
                parameters[2].RefKind == RefKind.Out)
            {
                return (true, true);
            }
        }

        return (false, false);
    }

    private static string? GetCommandDescription(INamedTypeSymbol classSymbol)
    {
        string? xmlDoc = classSymbol.GetDocumentationCommentXml();

        if (string.IsNullOrWhiteSpace(xmlDoc))
        {
            return null;
        }

        try
        {
            XElement element = XElement.Parse(xmlDoc);
            XElement? summaryNode = element.Element("summary");

            if (summaryNode != null)
            {
                var lines = summaryNode.Value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                return string.Join(" ", lines.Select(l => l.Trim()).Where(l => l.Length > 0));
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    #endregion

    #region ParserGenerator

    private static void GenerateParser(SourceProductionContext context, CommandModel command)
    {
        var code = new CodeBuilder();
        code.AppendLine("// <auto-generated/>");
        code.AppendLine();
        foreach (var @using in command.Usings)
        {
            code.AppendLine(@using);
        }
        code.AppendLine();
        code.AppendLine($"namespace {command.Namespace}");
        using (code.StartBlock())
        {
            code.AppendLine($"{command.Accessibility} static class {command.ClassName}Parser");
            using (code.StartBlock())
            {
                var arguments = command.Properties.OfType<ArgumentPropertyModel>().OrderBy(p => p.Position).ToList();
                var options = command.Properties.OfType<OptionPropertyModel>().OrderBy(p => p.OptionName).ToList();

                var argumentNameLength = arguments.Max(a => a.Name.Length) + 4;
                var optionNameLength = options.Max(o => o.OptionName.Length) + 4;

                code.AppendLine("private const global::System.String HelpArgumentAndOptions = @\"");
                if (arguments.Count != 0)
                {
                    code.AppendLine("Arguments:", applyIndent: false);
                    foreach (var argument in arguments)
                    {
                        code.Append("  ");
                        code.Append($"<{argument.Name}>".PadRight(argumentNameLength, ' '));
                        code.AppendLine(argument.Description, applyIndent: false);
                    }
                    code.AppendLine(applyIndent: false);
                }

                code.AppendLine("Options:", applyIndent: false);
                foreach (var option in options)
                {
                    if (option.Hidden)
                    {
                        continue;
                    }

                    if (option.ShortName is null)
                    {
                        code.Append("      ");
                    }
                    else
                    {
                        code.Append($"  -{option.ShortName}, ");
                    }

                    code.Append($"--{option.OptionName}".PadRight(optionNameLength, ' '));
                    code.Append(option.Description);

                    var metadata = new List<string>();
                    if (option.IsRequired)
                    {
                        metadata.Add("Required");
                    }

                    if (option.SpecialType != SpecialType.System_Boolean
                        &&
                        !string.IsNullOrWhiteSpace(option.DefaultValueString))
                    {
                        metadata.Add($"Default: {option.DefaultValueString}");
                    }

                    if (option.AllowedValueStrings.Length > 0)
                    {
                        metadata.Add($"Allowed: {string.Join(", ", option.AllowedValueStrings)}");
                    }

                    if (metadata.Count > 0)
                    {
                        if (!string.IsNullOrEmpty(option.Description))
                        {
                            code.Append(" ");
                        }

                        code.Append($"({string.Join("; ", metadata)})");
                    }

                    code.AppendLine(applyIndent: false);
                }

                code.Append("  -h, ");
                code.Append("--help".PadRight(optionNameLength, ' '));
                code.AppendLine("Displays this message.\";", applyIndent: false);

                code.AppendLine();
                code.AppendLine("public static global::System.String GetHelpText(global::System.String commandPath)");
                using (code.StartBlock())
                {
                    code.AppendLine($"return $@\"{command.Description}");
                    code.AppendLine(applyIndent: false);
                    code.AppendLine("Usage:", applyIndent: false);
                    code.Append("  {commandPath}");

                    foreach (var argument in arguments)
                    {
                        code.Append($" <{argument.Name}>");
                    }

                    if (options.Count != 0)
                    {
                        code.Append(" [options]");
                    }

                    code.AppendLine(applyIndent: false);
                    code.AppendLine("{HelpArgumentAndOptions}\";", applyIndent: false);
                }

                code.AppendLine();
                code.Append($"private static global::Kofoten.SimpleCli.CliParseResult ParseCore(global::System.ArraySegment<string> args", applyIndent: true);

                foreach (var ctorParam in command.ConstructorParameters)
                {
                    code.Append($", {ctorParam.TypeName} {ctorParam.Name}");
                }

                code.AppendLine($")", applyIndent: false);

                using (code.StartBlock())
                {
                    code.AppendLine("if (args.Array is null)");
                    using (code.StartBlock())
                    {
                        code.AppendLine("throw new global::System.ArgumentException(\"ArraySegment must reference a non-null array.\", nameof(args));");
                    }
                    code.AppendLine();

                    code.AppendLine("global::System.Collections.Generic.List<global::System.String> errors = new global::System.Collections.Generic.List<global::System.String>();");
                    code.AppendLine();

                    foreach (var arg in arguments)
                    {
                        code.AppendLine($"{arg.TypeName} arg_{arg.Name} = default;");
                    }

                    foreach (var opt in options)
                    {
                        if (opt.IsDictionary)
                        {
                            code.AppendLine($"global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<{opt.KeyTypeName}, {opt.ValueTypeName}>> opt_{opt.Name} = new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<{opt.KeyTypeName}, {opt.ValueTypeName}>>();");
                        }
                        else if (opt.IsCollection)
                        {
                            code.AppendLine($"global::System.Collections.Generic.List<{opt.ValueTypeName}> opt_{opt.Name} = new global::System.Collections.Generic.List<{opt.ValueTypeName}>();");
                        }
                        else if (opt.TypeName == "bool")
                        {
                            code.AppendLine($"bool opt_{opt.Name} = false;");
                        }
                        else if (!string.IsNullOrWhiteSpace(opt.DefaultValueSyntax))
                        {
                            code.AppendLine($"{opt.TypeName} opt_{opt.Name} = {opt.DefaultValueSyntax};");
                        }
                        else
                        {
                            code.AppendLine($"{opt.TypeName} opt_{opt.Name} = default!;");
                        }
                    }

                    code.AppendLine();
                    code.AppendLine("int segmentEnd = args.Offset + args.Count;");
                    code.AppendLine("int state = -1;");
                    code.AppendLine("int argIndex = 0;");
                    code.AppendLine("for (int i = args.Offset; i < segmentEnd; i++)");
                    using (code.StartBlock())
                    {
                        code.AppendLine("if (state > -2)");
                        using (code.StartBlock())
                        {
                            code.AppendLine("switch (args.Array[i])");
                            using (code.StartBlock())
                            {
                                code.AppendLine("case \"--\":");
                                using (code.Indent())
                                {
                                    code.AppendLine("state = -2;");
                                    code.AppendLine("break;");
                                }

                                for (int i = 0; i < options.Count; i++)
                                {
                                    var opt = options[i];

                                    if (!string.IsNullOrEmpty(opt.OptionName))
                                    {
                                        code.AppendLine($"case \"--{opt.OptionName}\":");
                                    }

                                    if (opt.ShortName.HasValue)
                                    {
                                        code.AppendLine($"case \"-{opt.ShortName}\":");
                                    }

                                    using (code.Indent())
                                    {
                                        code.AppendLine($"state = {i + 1};");
                                        if (opt.TypeName == "bool")
                                        {
                                            code.AppendLine($"opt_{opt.Name} = true;");
                                        }
                                        else if (opt.ImplicitValueString is not null)
                                        {
                                            GenerateImplicitValueAssignment(code, opt.ImplicitValueString, opt);
                                        }
                                        code.AppendLine("continue;");
                                    }
                                }

                                code.AppendLine("default:");
                                using (code.Indent())
                                {
                                    code.AppendLine("if (state == 0)");
                                    using (code.StartBlock())
                                    {
                                        code.AppendLine("errors.Add($\"Unknown option {args.Array[i]}\");");
                                    }
                                    code.AppendLine("break;");
                                }
                            }

                            code.AppendLine();
                            code.AppendLine("switch (state)");
                            using (code.StartBlock())
                            {
                                for (int i = 0; i < options.Count; i++)
                                {
                                    var opt = options[i];
                                    int stateId = i + 1;

                                    code.AppendLine($"case {stateId}:");
                                    using (code.Indent())
                                    {
                                        GenerateParser(code, opt);

                                        if (!opt.IsCollection && !opt.IsDictionary && !opt.IsFlagsEnum)
                                        {
                                            code.AppendLine("state = 0;");
                                        }

                                        code.AppendLine("break;");
                                    }
                                }

                                code.AppendLine("default:");
                                using (code.Indent())
                                {
                                    code.AppendLine("break;");
                                }
                            }
                        }

                        code.AppendLine();
                        code.AppendLine("if (state < 0)");
                        using (code.StartBlock())
                        {
                            code.AppendLine("switch (argIndex)");
                            using (code.StartBlock())
                            {
                                for (int i = 0; i < arguments.Count; i++)
                                {
                                    var arg = arguments[i];

                                    code.AppendLine($"case {i}:");
                                    using (code.Indent())
                                    {
                                        GenerateParser(code, arg);
                                        code.AppendLine("break;");
                                    }
                                }
                            }

                            code.AppendLine();
                            code.AppendLine("argIndex++;");
                        }
                    }

                    code.AppendLine($"if (argIndex < {arguments.Count})");
                    using (code.StartBlock())
                    {
                        code.AppendLine($"errors.Add($\"Too few arguments: At least {arguments.Count} argument(s) are required\");");
                    }

                    code.AppendLine();
                    code.AppendLine("if (errors.Count == 0)");
                    using (code.StartBlock())
                    {
                        var hasFinalizedCollections = false;
                        foreach (var collectionOpt in options.Where(o => o.IsCollection))
                        {
                            hasFinalizedCollections = true;

                            switch (collectionOpt.CollectionType)
                            {
                                case CollectionType.Array:
                                    code.AppendLine($"{collectionOpt.TypeName} finalOpt_{collectionOpt.Name} = opt_{collectionOpt.Name}.ToArray();");
                                    break;
                                case CollectionType.ConstructorCompatible:
                                    code.AppendLine($"{collectionOpt.TypeName} finalOpt_{collectionOpt.Name} = new {collectionOpt.TypeName}(opt_{collectionOpt.Name});");
                                    break;
                                case CollectionType.ImmutableArray:
                                    code.AppendLine($"{collectionOpt.TypeName} finalOpt_{collectionOpt.Name} = global::System.Collections.Immutable.ImmutableArray.CreateRange<{collectionOpt.ValueTypeName}>(opt_{collectionOpt.Name});");
                                    break;
                                case CollectionType.ImmutableList:
                                    code.AppendLine($"{collectionOpt.TypeName} finalOpt_{collectionOpt.Name} = global::System.Collections.Immutable.ImmutableList.CreateRange<{collectionOpt.ValueTypeName}>(opt_{collectionOpt.Name});");
                                    break;
                                case CollectionType.ImmutableHashSet:
                                    code.AppendLine($"{collectionOpt.TypeName} finalOpt_{collectionOpt.Name} = global::System.Collections.Immutable.ImmutableHashSet.CreateRange<{collectionOpt.ValueTypeName}>(opt_{collectionOpt.Name});");
                                    break;
                                case CollectionType.FrozenSet:
                                    code.AppendLine($"{collectionOpt.TypeName} finalOpt_{collectionOpt.Name} = global::System.Collections.Frozen.FrozenSet.ToFrozenSet<{collectionOpt.ValueTypeName}>(opt_{collectionOpt.Name});");
                                    break;
                            }
                        }

                        var hasFinalizedDictionaries = false;
                        foreach (var dictionaryOpt in options.Where(o => o.IsDictionary))
                        {
                            hasFinalizedDictionaries = true;

                            switch (dictionaryOpt.CollectionType)
                            {
                                case CollectionType.Array:
                                    code.AppendLine($"{dictionaryOpt.TypeName} finalOpt_{dictionaryOpt.Name} = opt_{dictionaryOpt.Name}.ToArray();");
                                    break;
                                case CollectionType.ConstructorCompatible:
                                    code.AppendLine($"{dictionaryOpt.TypeName} finalOpt_{dictionaryOpt.Name} = new {dictionaryOpt.TypeName}(opt_{dictionaryOpt.Name});");
                                    break;
                                case CollectionType.ImmutableArray:
                                    code.AppendLine($"{dictionaryOpt.TypeName} finalOpt_{dictionaryOpt.Name} = global::System.Collections.Immutable.ImmutableArray.CreateRange<global::System.Collections.Generic.KeyValuePair<{dictionaryOpt.KeyTypeName}, {dictionaryOpt.ValueTypeName}>>(opt_{dictionaryOpt.Name});");
                                    break;
                                case CollectionType.ImmutableList:
                                    code.AppendLine($"{dictionaryOpt.TypeName} finalOpt_{dictionaryOpt.Name} = global::System.Collections.Immutable.ImmutableList.CreateRange<global::System.Collections.Generic.KeyValuePair<{dictionaryOpt.KeyTypeName}, {dictionaryOpt.ValueTypeName}>>(opt_{dictionaryOpt.Name});");
                                    break;
                                case CollectionType.ImmutableHashSet:
                                    code.AppendLine($"{dictionaryOpt.TypeName} finalOpt_{dictionaryOpt.Name} = global::System.Collections.Immutable.ImmutableHashSet.CreateRange<global::System.Collections.Generic.KeyValuePair<{dictionaryOpt.KeyTypeName}, {dictionaryOpt.ValueTypeName}>>(opt_{dictionaryOpt.Name});");
                                    break;
                                case CollectionType.ImmutableDictionary:
                                    code.AppendLine($"{dictionaryOpt.TypeName} finalOpt_{dictionaryOpt.Name} = global::System.Collections.Immutable.ImmutableDictionary<{dictionaryOpt.KeyTypeName}, {dictionaryOpt.ValueTypeName}>.Empty.SetItems(opt_{dictionaryOpt.Name});");
                                    break;
                                case CollectionType.FrozenSet:
                                    code.AppendLine($"{dictionaryOpt.TypeName} finalOpt_{dictionaryOpt.Name} = global::System.Collections.Frozen.FrozenSet.ToFrozenSet<global::System.Collections.Generic.KeyValuePair<{dictionaryOpt.KeyTypeName}, {dictionaryOpt.ValueTypeName}>>(opt_{dictionaryOpt.Name});");
                                    break;
                                case CollectionType.FrozenDictionary:
                                    code.AppendLine($"{dictionaryOpt.TypeName} finalOpt_{dictionaryOpt.Name} = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary<{dictionaryOpt.KeyTypeName}, {dictionaryOpt.ValueTypeName}>(opt_{dictionaryOpt.Name});");
                                    break;
                                case CollectionType.DictionaryCompatible:
                                    code.AppendLine($"var finalOpt_{dictionaryOpt.Name} = global::Kofoten.SimpleCli.CliUtilities.CreateDictionaryWithOverwrite<{dictionaryOpt.KeyTypeName}, {dictionaryOpt.ValueTypeName}>(opt_{dictionaryOpt.Name});");
                                    break;
                            }
                        }

                        if (hasFinalizedCollections || hasFinalizedDictionaries)
                        {
                            code.AppendLine();
                        }

                        var ctorArgs = string.Join(", ", command.ConstructorParameters.Select(p => p.Name));
                        code.AppendLine($"var command = new {command.ClassName}({ctorArgs})");
                        code.AppendLine("{");
                        using (code.Indent())
                        {
                            foreach (var prop in command.Properties)
                            {
                                code.AppendLine(prop switch
                                {
                                    ArgumentPropertyModel apm => $"{prop.Name} = arg_{prop.Name},",
                                    OptionPropertyModel opm when IsFinalized(opm) => $"{prop.Name} = finalOpt_{prop.Name},",
                                    OptionPropertyModel opm => $"{prop.Name} = opt_{prop.Name},",
                                    _ => "// Unknown model",
                                });
                            }
                        }
                        code.AppendLine("};");

                        code.AppendLine();
                        if (command.HasValidationMethod)
                        {
                            code.AppendLine("var validationResult = command.Validate();");
                            code.AppendLine("switch (validationResult)");
                            using (code.StartBlock(addTrailingSemicolon: true))
                            {
                                code.AppendLine("case global::Kofoten.SimpleCli.CliValidationResult.Success _:");
                                using (code.Indent())
                                {
                                    code.AppendLine("return new global::Kofoten.SimpleCli.CliParseResult.Success(command);");
                                }
                                code.AppendLine("case global::Kofoten.SimpleCli.CliValidationResult.Failure f:");
                                using (code.Indent())
                                {
                                    code.AppendLine("return new global::Kofoten.SimpleCli.CliParseResult.Failure(f.Errors);");
                                }
                                code.AppendLine("default:");
                                using (code.Indent())
                                {
                                    code.AppendLine("throw new global::System.InvalidOperationException(\"Unexpected validation result.\");");
                                }
                            }
                        }
                        else
                        {
                            code.AppendLine("return new global::Kofoten.SimpleCli.CliParseResult.Success(command);");
                        }
                    }

                    code.AppendLine();
                    code.AppendLine("return new global::Kofoten.SimpleCli.CliParseResult.Failure(errors);");
                }

                code.AppendLine();
                code.Append($"{command.Accessibility} static {command.ClassName} Parse(global::System.String[] args", applyIndent: true);

                foreach (var ctorParam in command.ConstructorParameters)
                {
                    code.Append($", {ctorParam.TypeName} {ctorParam.Name}");
                }

                code.AppendLine($")", applyIndent: false);
                using (code.StartBlock())
                {
                    code.AppendLine("if (args is null)");
                    using (code.StartBlock())
                    {
                        code.AppendLine("throw new global::System.ArgumentNullException(nameof(args));");
                    }
                    code.AppendLine();

                    code.Append($"var result = ParseCore(new global::System.ArraySegment<global::System.String>(args)", applyIndent: true);

                    foreach (var ctorParam in command.ConstructorParameters)
                    {
                        code.Append($", {ctorParam.Name}");
                    }

                    code.AppendLine($");", applyIndent: false);

                    code.AppendLine("switch (result)");
                    using (code.StartBlock())
                    {
                        code.AppendLine("case global::Kofoten.SimpleCli.CliParseResult.Success success:");
                        using (code.Indent())
                        {
                            code.AppendLine($"return ({command.ClassName})success.Parsable;");
                        }
                        code.AppendLine("case global::Kofoten.SimpleCli.CliParseResult.Failure failure:");
                        using (code.Indent())
                        {
                            code.AppendLine("throw new CliParseException(failure.Errors, GetHelpText(global::System.String.Empty));");
                        }
                        code.AppendLine("default:");
                        using (code.Indent())
                        {
                            code.AppendLine("throw new global::System.InvalidOperationException(\"Unexpected parse result.\");");
                        }
                    }
                }

                code.AppendLine();
                code.Append($"{command.Accessibility} static void Map{command.ClassName}(this global::Kofoten.SimpleCli.CliCommandRouter<global::System.Func<global::Kofoten.SimpleCli.CliParseResult>> router, global::System.String verb", applyIndent: true);

                foreach (var ctorParam in command.ConstructorParameters)
                {
                    code.Append($", {ctorParam.TypeName} {ctorParam.Name}");
                }

                code.AppendLine($")", applyIndent: false);
                using (code.StartBlock())
                {
                    code.AppendLine("if (router is null)");
                    using (code.StartBlock())
                    {
                        code.AppendLine("throw new global::System.ArgumentNullException(nameof(router));");
                    }
                    code.AppendLine();
                    code.AppendLine("if (verb is null)");
                    using (code.StartBlock())
                    {
                        code.AppendLine("throw new global::System.ArgumentNullException(nameof(verb));");
                    }
                    code.AppendLine();

                    code.Append($"router.Map(verb, new {command.ClassName}Factory(", applyIndent: true);
                    if (command.ConstructorParameters.Count > 0)
                    {
                        code.Append($"{command.ConstructorParameters[0].Name}");
                        for (int i = 1; i < command.ConstructorParameters.Count; i++)
                        {
                            code.Append($", {command.ConstructorParameters[i].Name}");
                        }
                    }
                    code.AppendLine("));", applyIndent: false);
                }

                code.AppendLine();
                code.AppendLine($"private class {command.ClassName}Factory : global::Kofoten.SimpleCli.ICliCommandFactory<global::System.Func<global::Kofoten.SimpleCli.CliParseResult>>");
                using (code.StartBlock())
                {
                    foreach (var ctorParam in command.ConstructorParameters)
                    {
                        code.AppendLine($"private {ctorParam.TypeName} {ctorParam.Name};");
                    }

                    code.AppendLine();
                    code.AppendLine("public global::System.Boolean IsLeaf => true;");
                    code.AppendLine($"public global::System.String CommandDescription => \"{command.Description}\";");
                    code.AppendLine();

                    code.Append($"public {command.ClassName}Factory(", applyIndent: true);
                    if (command.ConstructorParameters.Count > 0)
                    {
                        code.Append($"{command.ConstructorParameters[0].TypeName} {command.ConstructorParameters[0].Name}");

                        for (int i = 1; i < command.ConstructorParameters.Count; i++)
                        {
                            code.Append($", {command.ConstructorParameters[i].TypeName} {command.ConstructorParameters[i].Name}");
                        }
                    }

                    code.AppendLine($")", applyIndent: false);
                    using (code.StartBlock())
                    {
                        foreach (var ctorParam in command.ConstructorParameters)
                        {
                            code.AppendLine($"this.{ctorParam.Name} = {ctorParam.Name};");
                        }
                    }

                    code.AppendLine();
                    code.AppendLine($"public global::System.Func<global::Kofoten.SimpleCli.CliParseResult> GetFactoryFunction(global::System.ArraySegment<string> args)");
                    using (code.StartBlock())
                    {
                        code.Append($"return () => {command.ClassName}Parser.ParseCore(args", applyIndent: true);

                        foreach (var ctorParam in command.ConstructorParameters)
                        {
                            code.Append($", {ctorParam.Name}");
                        }

                        code.AppendLine($");", applyIndent: false);
                    }
                    code.AppendLine();
                    code.AppendLine($"public global::System.String GetUsage(global::System.String commandPath) => {command.ClassName}Parser.GetHelpText(commandPath);");
                }

                if (command.HasDependencyInjection)
                {
                    code.AppendLine();
                    code.AppendLine($"{command.Accessibility} static void Map{command.ClassName}(this global::Kofoten.SimpleCli.CliCommandRouter<global::System.Func<global::System.IServiceProvider, global::Kofoten.SimpleCli.CliParseResult>> router, global::System.String verb)");
                    using (code.StartBlock())
                    {
                        code.AppendLine("if (router is null)");
                        using (code.StartBlock())
                        {
                            code.AppendLine("throw new global::System.ArgumentNullException(nameof(router));");
                        }
                        code.AppendLine("if (verb is null)");
                        using (code.StartBlock())
                        {
                            code.AppendLine("throw new global::System.ArgumentNullException(nameof(verb));");
                        }
                        code.AppendLine();
                        code.AppendLine($"router.Map(verb, new DependencyInjection{command.ClassName}Factory());");
                    }

                    code.AppendLine();
                    code.AppendLine($"private class DependencyInjection{command.ClassName}Factory : global::Kofoten.SimpleCli.ICliCommandFactory<global::System.Func<global::System.IServiceProvider, global::Kofoten.SimpleCli.CliParseResult>>");
                    using (code.StartBlock())
                    {
                        code.AppendLine("public global::System.Boolean IsLeaf => true;");
                        code.AppendLine($"public global::System.String CommandDescription => \"{command.Description}\";");
                        code.AppendLine();

                        code.AppendLine($"public DependencyInjection{command.ClassName}Factory()");
                        using (code.StartBlock())
                        {
                        }

                        code.AppendLine();
                        code.AppendLine($"public global::System.Func<global::System.IServiceProvider, global::Kofoten.SimpleCli.CliParseResult> GetFactoryFunction(global::System.ArraySegment<string> args)");
                        using (code.StartBlock())
                        {
                            code.AppendLine("return (sp) =>");
                            using (code.StartBlock(addTrailingSemicolon: true))
                            {
                                foreach (var ctorParam in command.ConstructorParameters)
                                {
                                    code.AppendLine($"var {ctorParam.Name} = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{ctorParam.TypeName}>(sp);");
                                }

                                code.AppendLine();
                                code.Append($"return {command.ClassName}Parser.ParseCore(args", applyIndent: true);
                                foreach (var ctorParam in command.ConstructorParameters)
                                {
                                    code.Append($", {ctorParam.Name}");
                                }
                                code.AppendLine($");", applyIndent: false);
                            }
                        }
                        code.AppendLine();
                        code.AppendLine($"public global::System.String GetUsage(global::System.String commandPath) => {command.ClassName}Parser.GetHelpText(commandPath);");
                    }
                }
            }
        }

        context.AddSource($"{command.ClassName}Parser.g.cs", code.ToString());
    }

    private static void GenerateParser(CodeBuilder code, PropertyModel model)
    {
        switch (model)
        {
            case ArgumentPropertyModel argModel:
                if (argModel.SpecialType == SpecialType.System_String)
                {
                    code.AppendLine($"arg_{argModel.Name} = args.Array[i];");
                    break;
                }
                else if (argModel.IsEnum)
                {
                    code.AppendLine($"if (!global::System.Enum.TryParse<{argModel.ValueTypeName}>(args.Array[i], true, out arg_{argModel.Name}))", applyIndent: true);
                    using (code.StartBlock())
                    {
                        code.AppendLine($"errors.Add(\"Argument {argModel.Name} can not be parsed to type: {argModel.ValueTypeName}\");");
                    }
                }
                else
                {
                    code.Append($"if (!{argModel.ValueParseMethodName}(args.Array[i], out arg_{argModel.Name}", applyIndent: true);
                    if (argModel.ValueHasErrorMessageOut)
                    {
                        code.AppendLine(", out global::System.String customError))", applyIndent: false);
                        using (code.StartBlock())
                        {
                            code.AppendLine($"errors.Add(\"Failed to parse argument {argModel.Name}: {{customError}}\");");
                        }
                    }
                    else
                    {
                        code.AppendLine("))", applyIndent: false);
                        using (code.StartBlock())
                        {
                            code.AppendLine($"errors.Add(\"Argument {argModel.Name} can not be parsed to type: {argModel.ValueTypeName}\");");
                        }
                    }
                }
                break;
            case OptionPropertyModel optModel:
                if (optModel.SpecialType == SpecialType.System_String)
                {
                    code.AppendLine($"opt_{optModel.Name} = args.Array[i];");
                    break;
                }

                if (optModel.ValueSpecialType == SpecialType.System_String && optModel.IsCollection)
                {
                    code.AppendLine($"opt_{optModel.Name}.Add(args.Array[i]);");
                    break;
                }

                using (code.StartBlock())
                {
                    IDisposable? dictionaryBlock = null;
                    if (optModel.IsEnum)
                    {
                        code.AppendLine($"if (global::System.Enum.TryParse<{optModel.ValueTypeName}>(args.Array[i], true, out {optModel.ValueTypeName} v))");
                    }
                    else
                    {
                        if (optModel.IsDictionary)
                        {
                            code.AppendLine("global::System.String currentArg = args.Array[i];");
                            code.AppendLine("global::System.Int32 delimiterIndex = currentArg.IndexOf(\"=\");");
                            code.AppendLine();
                            code.AppendLine("if (delimiterIndex == -1)");
                            using (code.StartBlock())
                            {
                                code.AppendLine($"errors.Add($\"Invalid format ({{args.Array[i]}}) for option '--{optModel.OptionName}' at position {{i}}. A key value pair must be delimitered using the equals sign.\");");
                            }
                            code.AppendLine("else");
                            dictionaryBlock = code.StartBlock();

                            code.AppendLine("global::System.String keyPart = currentArg.Substring(0, delimiterIndex);");
                            code.AppendLine("global::System.String valuePart = currentArg.Substring(delimiterIndex + 1);");
                            code.AppendLine("global::System.Boolean isValidKVP = true;");
                            code.AppendLine();

                            if (optModel.KeySpecialType != SpecialType.System_String)
                            {
                                code.Append($"if (!{optModel.KeyParseMethodName}(keyPart, out {optModel.KeyTypeName} k", applyIndent: true);

                                if (optModel.KeyHasErrorMessageOut)
                                {
                                    code.Append(", out global::System.String customError");
                                }

                                code.AppendLine("))", applyIndent: false);
                                using (code.StartBlock())
                                {
                                    if (optModel.KeyHasErrorMessageOut)
                                    {
                                        code.AppendLine($"errors.Add($\"Failed to parse key for option '--{optModel.OptionName}': {{customError}}\");");
                                    }
                                    else
                                    {
                                        code.AppendLine($"errors.Add($\"Invalid {optModel.KeyTypeName} key ({{args.Array[i]}}) for option '--{optModel.OptionName}' at position {{i}}.\");");
                                    }
                                    code.AppendLine("isValidKVP = false;");
                                }

                                code.AppendLine();
                            }

                            if (optModel.ValueSpecialType != SpecialType.System_String)
                            {
                                code.Append($"if (!{optModel.ValueParseMethodName}(valuePart, out {optModel.ValueTypeName} v", applyIndent: true);

                                if (optModel.ValueHasErrorMessageOut)
                                {
                                    if (optModel.KeyHasErrorMessageOut && optModel.KeySpecialType != SpecialType.System_String)
                                    {
                                        code.Append(", out customError");
                                    }
                                    else
                                    {
                                        code.Append(", out global::System.String customError");
                                    }
                                }

                                code.AppendLine("))", applyIndent: false);
                                using (code.StartBlock())
                                {
                                    if (optModel.ValueHasErrorMessageOut)
                                    {
                                        code.AppendLine($"errors.Add($\"Failed to parse option '--{optModel.OptionName}': {{customError}}\");");
                                    }
                                    else
                                    {
                                        code.AppendLine($"errors.Add($\"Invalid {optModel.ValueTypeName} value ({{args.Array[i]}}) for option '--{optModel.OptionName}' at position {{i}}.\");");
                                    }
                                    code.AppendLine("isValidKVP = false;");
                                }

                                code.AppendLine();
                            }

                            code.AppendLine("if (isValidKVP)");
                        }
                        else
                        {
                            code.Append($"if ({optModel.ValueParseMethodName}(args.Array[i], out {optModel.ValueTypeName} v", applyIndent: true);

                            if (optModel.ValueHasErrorMessageOut)
                            {
                                code.Append(", out global::System.String customError");
                            }

                            code.AppendLine("))", applyIndent: false);
                        }
                    }

                    using (code.StartBlock())
                    {
                        if (model.IsFlagsEnum)
                        {
                            code.AppendLine($"opt_{optModel.Name} |= v;");
                        }
                        else if (model.IsDictionary)
                        {
                            var keyName = optModel.KeySpecialType == SpecialType.System_String ? "keyPart" : "k";
                            var valueName = optModel.ValueSpecialType == SpecialType.System_String ? "valuePart" : "v";

                            code.AppendLine($"opt_{optModel.Name}.Add(new global::System.Collections.Generic.KeyValuePair<{optModel.KeyTypeName}, {optModel.ValueTypeName}>({keyName}, {valueName}));");
                        }
                        else if (model.IsCollection)
                        {
                            code.AppendLine($"opt_{optModel.Name}.Add(v);");
                        }
                        else
                        {
                            code.AppendLine($"opt_{optModel.Name} = v;");
                        }
                    }

                    if (!optModel.IsDictionary)
                    {
                        code.AppendLine("else");
                        using (code.StartBlock())
                        {
                            if (optModel.ValueHasErrorMessageOut)
                            {
                                code.AppendLine($"errors.Add($\"Failed to parse option '--{optModel.OptionName}': {{customError}}\");");
                            }
                            else
                            {
                                code.AppendLine($"errors.Add($\"Invalid {optModel.ValueTypeName} value ({{args.Array[i]}}) for option '--{optModel.OptionName}' at position {{i}}.\");");
                            }
                        }
                    }

                    dictionaryBlock?.Dispose();
                }
                break;
            default:
                break;
        }
    }

    private static void GenerateImplicitValueAssignment(CodeBuilder code, string implicitValueString, OptionPropertyModel model)
    {
        switch (model.ValueSpecialType)
        {
            case SpecialType.System_String:
                code.AppendLine($"opt_{model.Name} = {SymbolDisplay.FormatLiteral(implicitValueString, quote: true)};");
                break;
            case SpecialType.System_Char:
                char charVal = implicitValueString.Length > 0 ? implicitValueString[0] : '\0';
                code.AppendLine($"opt_{model.Name} = {SymbolDisplay.FormatLiteral(charVal, quote: true)};");
                break;
            case SpecialType.System_Boolean:
                code.AppendLine($"opt_{model.Name} = {implicitValueString.ToLowerInvariant()};");
                break;
            case SpecialType.System_Decimal:
                code.AppendLine($"opt_{model.Name} = {implicitValueString}m;");
                break;
            case SpecialType.System_Single:
                code.AppendLine($"opt_{model.Name} = {implicitValueString}f;");
                break;
            case SpecialType.System_Byte:
            case SpecialType.System_Double:
            case SpecialType.System_Int16:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
                code.AppendLine($"opt_{model.Name} = {implicitValueString};");
                break;
            default:
                using (code.StartBlock())
                {
                    if (model.ValueHasErrorMessageOut)
                    {
                        code.AppendLine($"if (!{model.ValueParseMethodName}(\"{implicitValueString}\", out opt_{model.Name}, out global::System.String customError))");
                        using (code.StartBlock())
                        {
                            code.AppendLine($"errors.Add($\"Failed to parse option '--{model.OptionName}': {{customError}}\");");
                        }
                    }
                    else
                    {
                        code.AppendLine($"if (!{model.ValueParseMethodName}(\"{implicitValueString}\", out opt_{model.Name}))");
                        using (code.StartBlock())
                        {
                            code.AppendLine($"errors.Add(\"Invalid {model.ValueTypeName} value ('{implicitValueString}') for option '--{model.OptionName}'.\");");
                        }
                    }
                }
                break;
        }
    }

    private static bool IsFinalized(PropertyModel model)
    {

        return model.CollectionType != CollectionType.None
            &&
            model.CollectionType != CollectionType.ListCompatible;
    }

    #endregion
}

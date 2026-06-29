using Kofoten.SimpleCli.Generator.Data;
using Kofoten.SimpleCli.Generator.Diagnostics;
using Microsoft.CodeAnalysis;
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
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c && c.BaseList is not null,
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node);

        var simpleCliCompilationContextProvider = context.CompilationProvider.Select(static (compilation, _) =>
        {
            var diRouterSymbol = compilation.GetTypeByMetadataName("Kofoten.SimpleCli.DependencyInjection.DependencyInjectionCliCommandRouter");
            var serviceProviderSymbol = compilation.GetTypeByMetadataName("System.IServiceProvider");
            var getRequiredServiceExtensionsSymbol = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");

            var hasDependencyInjection =
                diRouterSymbol is not null
                &&
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
                CliArgumentAttributeSymbol: compilation.GetTypeByMetadataName("Kofoten.SimpleCli.CliArgumentAttribute"),
                CliOptionAttributeSymbol: compilation.GetTypeByMetadataName("Kofoten.SimpleCli.CliOptionAttribute"),
                FlagsAttributeSymbol: compilation.GetTypeByMetadataName("System.FlagsAttribute"),
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

        var compilation = semanticModel.Compilation;

        var inheritsCommand = classSymbol.AllInterfaces.Any(interfaceSymbol =>
            SymbolEqualityComparer.Default.Equals(interfaceSymbol, simpleCliContext.CliParsableSymbol));

        if (!inheritsCommand)
        {
            return new CommandGenerationResult(null, diagnostics.ToImmutable());
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

        var properties = new List<PropertyModel>();
        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var argAttribute = member.GetAttributes().FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, simpleCliContext.CliArgumentAttributeSymbol));

            var optAttribute = member.GetAttributes().FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, simpleCliContext.CliOptionAttributeSymbol));

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

                    return new CommandGenerationResult(null, diagnostics.ToImmutable());
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
                        // TODO: Diagnostig unsupported collection type.
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
                        // TODO: Diagnostig unsupported collection type.
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

            if (isEnum)
            {
                valueHasValidParser = true;
                isFlagsEnum = valueTypeSymbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, simpleCliContext.FlagsAttributeSymbol));
            }
            else if (!isString)
            {
                string targetMethodName = "TryParse";
                (valueHasValidParser, valueHasErrorMessageOut) = InspectParserSignature(valueTypeSymbol, targetMethodName);
                valueParserMethodName = $"{valueTypeName}.{targetMethodName}";

                if (isDictionary)
                {
                    (keyHasValidParser, keyHasErrorMessageOut) = InspectParserSignature(keyTypeSymbol!, targetMethodName);
                    keyParserMethodName = $"{keyTypeName}.{targetMethodName}";
                }
            }

            if (!valueHasValidParser)
            {
                // TODO: Emit Diagnostic Error for DX: "Type {valueTypeSymbol.Name} does not have a valid parser."
            }

            if (!keyHasValidParser && isDictionary)
            {
                // TODO: Emit Diagnostic Error for DX: "Type {keyTypeSymbol.Name} does not have a valid parser."
            }

            string? defaultValueString = null;
            var syntaxReference = member.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxReference != null)
            {
                var syntaxNode = syntaxReference.GetSyntax();
                if (syntaxNode is PropertyDeclarationSyntax propertySyntax
                    &&
                    propertySyntax.Initializer != null)
                {
                    defaultValueString = propertySyntax.Initializer.Value.ToString();
                }
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

                var descriptionArg = optAttribute.NamedArguments.FirstOrDefault(na => na.Key == "Description");
                var description = descriptionArg.Value.Value is string d ? d : string.Empty;

                properties.Add(new OptionPropertyModel(
                    Name: member.Name,
                    TypeName: typeName,
                    ValueTypeName: valueTypeName,
                    KeyTypeName: keyTypeName,
                    SpecialType: valueTypeSymbol.SpecialType,
                    IsRequired: member.IsRequired,
                    Description: description,
                    ValueParseMethodName: valueParserMethodName,
                    ValueHasErrorMessageOut: valueHasErrorMessageOut,
                    KeyParseMethodName: keyParserMethodName,
                    KeyHasErrorMessageOut: keyHasErrorMessageOut,
                    DefaultValueString: defaultValueString,
                    OptionName: optName,
                    ShortName: shortName,
                    IsCollection: isCollection,
                    CollectionType: collectionType,
                    IsDictionary: isDictionary,
                    IsEnum: isEnum,
                    IsFlagsEnum: isFlagsEnum));
            }
        }

        var propertySymbolsByName = classSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .ToDictionary(p => p.Name, p => p);

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

        if (diagnostics.Count > 0)
        {
            return new CommandGenerationResult(null, diagnostics.ToImmutable());
        }

        var command = new CommandModel(
            Namespace: classSymbol.ContainingNamespace.ToDisplayString(),
            ClassName: classSymbol.Name,
            Description: GetCommandDescription(classSymbol),
            ConstructorParameters: constructorParams,
            Properties: properties,
            HasDependencyInjection: simpleCliContext.HasDependencyInjection,
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
            code.AppendLine($"public static class {command.ClassName}Parser");
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
                    if (option.IsRequired)
                    {
                        code.AppendLine(" [Required]", applyIndent: false);
                    }
                    else if (option.SpecialType == SpecialType.System_Boolean)
                    {
                        code.AppendLine(applyIndent: false);
                    }
                    else if (!string.IsNullOrWhiteSpace(option.DefaultValueString))
                    {
                        var defaultValue = option.DefaultValueString!.Trim('"', '\'').Replace("\"", "\"\"");
                        code.AppendLine($" [Default: {defaultValue}]", applyIndent: false);
                    }
                    else
                    {
                        code.AppendLine(applyIndent: false);
                    }
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
                        code.AppendLine($"if (args.Count > {arg.Position})");
                        using (code.StartBlock())
                        {
                            if (arg.SpecialType == SpecialType.System_String)
                            {
                                code.AppendLine($"arg_{arg.Name} = args.Array[args.Offset + {arg.Position}];");
                            }
                            else
                            {
                                TryParseParserGenerator(code, arg);
                            }
                        }

                        if (arg.IsRequired)
                        {
                            code.AppendLine("else");
                            using (code.StartBlock())
                            {
                                code.AppendLine($"errors.Add(\"Missing required argument {arg.Name}\");");
                            }
                        }

                        code.AppendLine();
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
                        else if (!string.IsNullOrWhiteSpace(opt.DefaultValueString))
                        {
                            code.AppendLine($"{opt.TypeName} opt_{opt.Name} = {opt.DefaultValueString};");
                        }
                        else
                        {
                            code.AppendLine($"{opt.TypeName} opt_{opt.Name} = default!;");
                        }
                    }

                    code.AppendLine();
                    code.AppendLine("int state = 0;");
                    code.AppendLine($"for (int i = {arguments.Count}; i < args.Count; i++)");
                    using (code.StartBlock())
                    {
                        code.AppendLine("switch (args.Array[args.Offset + i])");
                        using (code.StartBlock())
                        {
                            for (int i = 0; i < options.Count; i++)
                            {
                                var opt = options[i];
                                int stateId = i + 1;

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
                                    code.AppendLine($"state = {stateId};");
                                    if (opt.TypeName == "bool")
                                    {
                                        code.AppendLine($"opt_{opt.Name} = true;");
                                    }
                                    code.AppendLine("continue;");
                                }
                            }


                            code.AppendLine("default:");
                            using (code.Indent())
                            {
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
                                    if (opt.SpecialType == SpecialType.System_String)
                                    {
                                        if (opt.IsCollection)
                                        {
                                            code.AppendLine($"opt_{opt.Name}.Add(args.Array[args.Offset + i]);");
                                        }
                                        else
                                        {
                                            code.AppendLine($"opt_{opt.Name} = args.Array[args.Offset + i];");
                                        }
                                    }
                                    else
                                    {
                                        using (code.StartBlock())
                                        {
                                            TryParseParserGenerator(code, opt);
                                        }
                                    }

                                    if (!opt.IsCollection && !opt.IsFlagsEnum)
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
                        code.AppendLine("return new global::Kofoten.SimpleCli.CliParseResult.Success(command);");
                    }

                    code.AppendLine();
                    code.AppendLine("return new global::Kofoten.SimpleCli.CliParseResult.Failure(errors);");
                }

                code.AppendLine();
                code.Append($"public static {command.ClassName} Parse(global::System.String[] args", applyIndent: true);

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
                            code.AppendLine("global::System.Text.StringBuilder messageBuilder = new global::System.Text.StringBuilder();");
                            code.AppendLine("messageBuilder.AppendLine(\"Failed to parse arguments:\");");
                            code.AppendLine("foreach (var error in failure.Errors)");
                            using (code.StartBlock())
                            {
                                code.AppendLine("messageBuilder.AppendLine($\"\\t{error}\");");
                            }
                            code.AppendLine("throw new ArgumentException(messageBuilder.ToString());");
                        }
                        code.AppendLine("break;");
                    }

                    code.AppendLine("throw new global::System.InvalidOperationException(\"Unexpected parse result.\");");
                }

                code.AppendLine();
                code.Append($"public static void Map{command.ClassName}(this global::Kofoten.SimpleCli.CliCommandRouter router, global::System.String verb", applyIndent: true);

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
                code.AppendLine($"public class {command.ClassName}Factory : global::Kofoten.SimpleCli.ICliCommandFactory<global::System.Func<global::Kofoten.SimpleCli.CliParseResult>>");
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
                    code.AppendLine($"public static void Map{command.ClassName}(this global::Kofoten.SimpleCli.DependencyInjection.DependencyInjectionCliCommandRouter router, global::System.String verb)");
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
                    code.AppendLine($"public class DependencyInjection{command.ClassName}Factory : global::Kofoten.SimpleCli.ICliCommandFactory<global::System.Func<global::System.IServiceProvider, global::Kofoten.SimpleCli.CliParseResult>>");
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

    private static void TryParseParserGenerator(CodeBuilder code, PropertyModel model)
    {
        switch (model)
        {
            case ArgumentPropertyModel argModel:
                if (argModel.IsEnum)
                {
                    code.AppendLine($"if (!global::System.Enum.TryParse<{argModel.ValueTypeName}>(args.Array[args.Offset + {argModel.Position}], true, out arg_{argModel.Name}))", applyIndent: true);
                    using (code.StartBlock())
                    {
                        code.AppendLine($"errors.Add(\"Argument {argModel.Name} can not be parsed to type: {argModel.ValueTypeName}\");");
                    }
                }
                else
                {
                    code.Append($"if (!{argModel.ValueParseMethodName}(args.Array[args.Offset + {argModel.Position}], out arg_{argModel.Name}", applyIndent: true);
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
                IDisposable? dictionaryBlock = null;

                if (optModel.IsEnum)
                {
                    code.AppendLine($"if (global::System.Enum.TryParse<{optModel.ValueTypeName}>(args.Array[args.Offset + i], true, out {optModel.ValueTypeName} v))");
                }
                else
                {
                    var valueAccessor = "args.Array[args.Offset + i]";

                    if (optModel.IsDictionary)
                    {
                        valueAccessor = "valuePart";

                        code.AppendLine("global::System.String currentArg = args.Array[args.Offset + i];");
                        code.AppendLine("global::System.Int32 delimiterIndex = currentArg.IndexOf(\"=\");");
                        code.AppendLine();
                        code.AppendLine("if (delimiterIndex == -1)");
                        using (code.StartBlock())
                        {
                            code.AppendLine($"errors.Add($\"Invalid format ({{args.Array[args.Offset + i]}}) for option '--{optModel.OptionName}' at position {{i}}. A key value pair must be delimitered using the equals sign.\");");
                        }
                        code.AppendLine("else");
                        dictionaryBlock = code.StartBlock();

                        code.AppendLine("global::System.String keyPart = currentArg.Substring(0, delimiterIndex);");
                        code.AppendLine("global::System.String valuePart = currentArg.Substring(delimiterIndex + 1);");
                        code.AppendLine("global::System.Boolean isValidKVP = true;");
                        code.AppendLine();
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
                                code.AppendLine($"errors.Add($\"Invalid {optModel.KeyTypeName} key ({{args.Array[args.Offset + i]}}) for option '--{optModel.OptionName}' at position {{i}}.\");");
                            }
                            code.AppendLine("isValidKVP = false;");
                        }
                        code.AppendLine();
                        code.Append($"if (!", applyIndent: true);
                    }
                    else
                    {
                        code.Append($"if (", applyIndent: true);
                    }

                    code.Append($"{optModel.ValueParseMethodName}({valueAccessor}, out {optModel.ValueTypeName} v");
                    if (optModel.ValueHasErrorMessageOut)
                    {
                        if (optModel.KeyHasErrorMessageOut)
                        {
                            code.Append(", out customError");
                        }
                        else
                        {
                            code.Append(", out global::System.String customError");
                        }
                    }
                    code.AppendLine("))", applyIndent: false);

                    if (optModel.IsDictionary)
                    {
                        using (code.StartBlock())
                        {
                            if (optModel.ValueHasErrorMessageOut)
                            {
                                code.AppendLine($"errors.Add($\"Failed to parse option '--{optModel.OptionName}': {{customError}}\");");
                            }
                            else
                            {
                                code.AppendLine($"errors.Add($\"Invalid {optModel.ValueTypeName} value ({{args.Array[args.Offset + i]}}) for option '--{optModel.OptionName}' at position {{i}}.\");");
                            }
                            code.AppendLine("isValidKVP = false;");
                        }
                        code.AppendLine();
                        code.AppendLine("if (isValidKVP)");

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
                        code.AppendLine($"opt_{optModel.Name}.Add(new global::System.Collections.Generic.KeyValuePair<{optModel.KeyTypeName}, {optModel.ValueTypeName}>(k, v));");
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
                            code.AppendLine($"errors.Add($\"Invalid {optModel.ValueTypeName} value ({{args.Array[args.Offset + i]}}) for option '--{optModel.OptionName}' at position {{i}}.\");");
                        }
                    }
                }

                dictionaryBlock?.Dispose();
                break;
            default:
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

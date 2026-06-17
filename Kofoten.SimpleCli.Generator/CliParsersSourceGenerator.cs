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
        var commandModels = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c && c.BaseList is not null,
                transform: static (ctx, _) => GetCommandTarget(ctx));

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

    private static CommandGenerationResult GetCommandTarget(GeneratorSyntaxContext context)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
        {
            return new CommandGenerationResult(null, diagnostics.ToImmutable());
        }

        var compilation = context.SemanticModel.Compilation;

        var diRouterSymbol = compilation.GetTypeByMetadataName("Kofoten.SimpleCli.DependencyInjection.DependencyInjectionCliCommandRouter");
        var serviceProviderSymbol = compilation.GetTypeByMetadataName("System.IServiceProvider");
        var getRequiredServiceExtensionsSymbol = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");

        var hasDependencyInjection =
            diRouterSymbol is not null &&
            serviceProviderSymbol is not null &&
            getRequiredServiceExtensionsSymbol is not null;

        var parsableCommandSymbol = compilation.GetTypeByMetadataName("Kofoten.SimpleCli.ICliParsable");

        var inheritsCommand = classSymbol.AllInterfaces.Any(interfaceSymbol =>
            SymbolEqualityComparer.Default.Equals(interfaceSymbol, parsableCommandSymbol));

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

        var argAttributeSymbol = compilation.GetTypeByMetadataName("Kofoten.SimpleCli.CliArgumentAttribute");
        var optAttributeSymbol = compilation.GetTypeByMetadataName("Kofoten.SimpleCli.CliOptionAttribute");

        var enumerableSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");

        var properties = new List<PropertyModel>();
        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var argAttribute = member.GetAttributes().FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, argAttributeSymbol));

            var optAttribute = member.GetAttributes().FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, optAttributeSymbol));

            string typeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            ITypeSymbol parseTypeSymbol = member.Type;

            bool isCollection = false;
            if (TryGetEnumerableElementType(member.Type, compilation, out var elementType))
            {
                if (elementType is null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedCollectionElementType,
                        member.Locations.FirstOrDefault() ?? classDecl.Identifier.GetLocation(),
                        member.Name));

                    return new CommandGenerationResult(null, diagnostics.ToImmutable());
                }

                parseTypeSymbol = elementType;
                isCollection = true;
            }

            bool isString = parseTypeSymbol.SpecialType == SpecialType.System_String;
            string parseTypeName = parseTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string parserMethodName = string.Empty;
            bool isValidParser = false;
            bool hasErrorMessageOut = false;

            if (!isString)
            {
                string targetMethodName = "TryParse";
                (isValidParser, hasErrorMessageOut) = InspectParserSignature(parseTypeSymbol, targetMethodName);
                parserMethodName = $"{parseTypeName}.{targetMethodName}";
            }

            if (!isValidParser)
            {
                // TODO: Emit Diagnostic Error for DX: "Type {parseTypeSymbol.Name} does not have a valid parser."
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
                    ParseTypeName: parseTypeName,
                    SpecialType: parseTypeSymbol.SpecialType,
                    IsRequired: member.IsRequired,
                    Description: description,
                    ParseMethodName: parserMethodName,
                    HasErrorMessageOut: hasErrorMessageOut,
                    Position: position));
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
                    ParseTypeName: parseTypeName,
                    SpecialType: parseTypeSymbol.SpecialType,
                    IsRequired: member.IsRequired,
                    Description: description,
                    ParseMethodName: parserMethodName,
                    HasErrorMessageOut: hasErrorMessageOut,
                    OptionName: optName,
                    ShortName: shortName,
                    IsCollection: isCollection));
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
            HasDependencyInjection: hasDependencyInjection);

        return new CommandGenerationResult(command, diagnostics.ToImmutable());
    }

    private static bool TryGetEnumerableElementType(
        ITypeSymbol type,
        Compilation compilation,
        out ITypeSymbol? elementType)
    {
        elementType = null;

        var ienumerableOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
        if (ienumerableOfT is null)
        {
            return false;
        }

        if (type is INamedTypeSymbol named &&
            named.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, ienumerableOfT))
        {
            elementType = named.TypeArguments[0];
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (iface is INamedTypeSymbol i &&
                i.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, ienumerableOfT))
            {
                elementType = i.TypeArguments[0];
                return true;
            }
        }

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
        code.AppendLine("using System;");
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

                code.AppendLine("private const string HelpArgumentAndOptions = @\"");
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
                    code.AppendLine(option.Description, applyIndent: false);
                }

                code.Append("  -h, ");
                code.Append("--help".PadRight(optionNameLength, ' '));
                code.AppendLine("Displays this message.\";", applyIndent: false);

                code.AppendLine();
                code.AppendLine("public static string GetHelpText(string commandPath)");
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
                code.Append($"private static {command.ClassName} ParseCore(global::System.ArraySegment<string> args", applyIndent: true);

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

                    code.AppendLine("global::System.Collections.Generic.List<string> errors = new global::System.Collections.Generic.List<string>();");
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
                        if (opt.IsCollection)
                        {
                            code.AppendLine($"global::System.Collections.Generic.List<{opt.ParseTypeName}> opt_{opt.Name} = new global::System.Collections.Generic.List<{opt.ParseTypeName}>();");
                        }
                        else if (opt.TypeName == "bool")
                        {
                            code.AppendLine($"bool opt_{opt.Name} = false;");
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

                                    if (!opt.IsCollection)
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
                        var ctorArgs = string.Join(", ", command.ConstructorParameters.Select(p => p.Name));
                        code.AppendLine($"return new {command.ClassName}({ctorArgs})");
                        code.AppendLine("{");
                        using (code.Indent())
                        {
                            foreach (var prop in command.Properties)
                            {
                                code.AppendLine(prop switch
                                {
                                    ArgumentPropertyModel apm => $"{prop.Name} = arg_{prop.Name},",
                                    OptionPropertyModel opm => $"{prop.Name} = opt_{prop.Name},",
                                    _ => "// Unknown model",
                                });
                            }
                        }
                        code.AppendLine("};");
                    }

                    code.AppendLine();
                    code.AppendLine("global::System.Text.StringBuilder messageBuilder = new global::System.Text.StringBuilder();");
                    code.AppendLine("messageBuilder.AppendLine(\"Failed to parse arguments:\");");
                    code.AppendLine("foreach (string error in errors)");
                    using (code.StartBlock())
                    {
                        code.AppendLine("messageBuilder.AppendLine($\"\\t{error}\");");
                    }
                    code.AppendLine("throw new ArgumentException(messageBuilder.ToString());");
                }

                code.AppendLine();
                code.Append($"public static {command.ClassName} Parse(string[] args", applyIndent: true);

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

                    code.Append($"return ParseCore(new global::System.ArraySegment<string>(args)", applyIndent: true);

                    foreach (var ctorParam in command.ConstructorParameters)
                    {
                        code.Append($", {ctorParam.Name}");
                    }

                    code.AppendLine($");", applyIndent: false);
                }

                code.AppendLine();
                code.Append($"public static void Map{command.ClassName}(this global::Kofoten.SimpleCli.CliCommandRouter router, string verb", applyIndent: true);

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

                    code.Append($"router.Map(verb, \"{command.Description}\", (args) => ParseCore(args", applyIndent: true);

                    foreach (var ctorParam in command.ConstructorParameters)
                    {
                        code.Append($", {ctorParam.Name}");
                    }

                    code.AppendLine($"), GetHelpText);", applyIndent: false);
                }

                if (command.HasDependencyInjection)
                {
                    code.AppendLine();
                    code.AppendLine($"public static void Map{command.ClassName}(this global::Kofoten.SimpleCli.DependencyInjection.DependencyInjectionCliCommandRouter router, string verb)");
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

                        code.Append($"router.Map(verb, (args, sp) => ParseCore(args", applyIndent: true);

                        foreach (var ctorParam in command.ConstructorParameters)
                        {
                            code.AppendLine(",", applyIndent: false);
                            using (code.Indent())
                            {
                                code.Append($"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{ctorParam.TypeName}>(sp)", applyIndent: true);
                            }
                        }

                        code.AppendLine("));", applyIndent: false);
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
                code.Append($"if (!{argModel.ParseMethodName}(args.Array[args.Offset + {argModel.Position}], out arg_{argModel.Name}", applyIndent: true);
                if (argModel.HasErrorMessageOut)
                {
                    code.AppendLine(", out string customError))", applyIndent: false);
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
                        code.AppendLine($"errors.Add(\"Argument {argModel.Name} can not be parsed to type: {argModel.ParseTypeName}\");");
                    }
                }
                break;
            case OptionPropertyModel optModel:
                code.Append($"if ({optModel.ParseMethodName}(args.Array[args.Offset + i], out {optModel.ParseTypeName} v", applyIndent: true);
                if (optModel.HasErrorMessageOut)
                {
                    code.Append(", out string customError");
                }
                code.AppendLine("))", applyIndent: false);
                using (code.StartBlock())
                {
                    if (model.IsCollection)
                    {
                        code.AppendLine($"opt_{optModel.Name}.Add(v);");
                    }
                    else
                    {
                        code.AppendLine($"opt_{optModel.Name} = v;");
                    }
                }
                code.AppendLine("else");
                using (code.StartBlock())
                {
                    if (optModel.HasErrorMessageOut)
                    {
                        code.AppendLine($"errors.Add($\"Failed to parse option '--{optModel.OptionName}': {{customError}}\");");
                    }
                    else
                    {
                        code.AppendLine($"errors.Add($\"Invalid {optModel.ParseTypeName} value ({{args.Array[args.Offset + i]}}) for option '--{optModel.OptionName}' at position {{i}}.\");");
                    }
                }
                break;
            default:
                break;
        }
    }

    #endregion
}

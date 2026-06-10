using Kofoten.SimpleCli.Generator.Data;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace Kofoten.SimpleCli.Generator;

[Generator]
public class CliParsersSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var commandModels = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c && c.BaseList is not null,
                transform: static (ctx, _) => GetCommandTarget(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(commandModels, static (spc, source) => GenerateParser(spc, source!));
    }

    #region BuildCommandModel

    private static CommandModel? GetCommandTarget(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        var compilation = context.SemanticModel.Compilation;
        var syncCommandSymbol = compilation.GetTypeByMetadataName("Kofoten.SimpleCli.ICliCommand");
        var asyncCommandSymbol = compilation.GetTypeByMetadataName("Kofoten.SimpleCli.IAsyncCliCommand");

        var inheritsCommand = classSymbol.AllInterfaces.Any(interfaceSymbol =>
            SymbolEqualityComparer.Default.Equals(interfaceSymbol, syncCommandSymbol)
            ||
            SymbolEqualityComparer.Default.Equals(interfaceSymbol, asyncCommandSymbol));

        if (!inheritsCommand)
        {
            return null;
        }

        var publicConstructors = classSymbol.Constructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        if (publicConstructors.Count != 1)
        {
            // Enforce opinion: We only support exactly ONE public constructor.
            // TODO: Emit Diagnostic descriptor here for better DX.
            return null;
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
            string parseTypeName = typeName;

            bool isCollection = false;
            if (TryGetEnumerableElementType(member.Type, compilation, out var elementType))
            {
                if (elementType is null)
                {
                    // TODO: Emit Diagnostic descriptor here for better DX.
                    return null;
                }

                parseTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                isCollection = true;
            }

            if (argAttribute != null
                &&
                argAttribute.ConstructorArguments.Length > 0
                &&
                argAttribute.ConstructorArguments[0].Value is int position)
            {
                properties.Add(new ArgumentPropertyModel(
                    Name: member.Name,
                    TypeName: typeName,
                    ParseTypeName: parseTypeName,
                    IsRequired: member.IsRequired,
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

                properties.Add(new OptionPropertyModel(
                    Name: member.Name,
                    TypeName: typeName,
                    ParseTypeName: parseTypeName,
                    IsRequired: member.IsRequired,
                    OptionName: optName,
                    ShortName: shortName,
                    IsCollection: isCollection));
            }
        }

        return new CommandModel(
            Namespace: classSymbol.ContainingNamespace.ToDisplayString(),
            ClassName: classSymbol.Name,
            ConstructorParameters: constructorParams,
            Properties: properties
        );
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

    #endregion

    #region ParserGenerator

    private static void GenerateParser(SourceProductionContext context, CommandModel command)
    {
        var code = new CodeBuilder();
        code.AppendLine("// <auto-generated/>");
        code.AppendLine();
        code.AppendLine("#nullable enable");
        code.AppendLine();
        code.AppendLine("using System;");
        code.AppendLine("using System.Collections.Generic;");
        code.AppendLine();
        code.AppendLine($"namespace {command.Namespace};");
        code.AppendLine();
        code.AppendLine($"public static class {command.ClassName}Parser");

        using (code.StartBlock())
        {
            code.Append($"public static {command.ClassName} Parse(string[] args", applyIndent: true);

            foreach (var ctorParam in command.ConstructorParameters)
            {
                code.Append($", {ctorParam.TypeName} {ctorParam.Name}");
            }

            code.AppendLine($")", applyIndent: false);

            using (code.StartBlock())
            {
                code.AppendLine("List<string> errors = [];");
                code.AppendLine();

                var arguments = command.Properties.OfType<ArgumentPropertyModel>().OrderBy(p => p.Position).ToList();
                foreach (var arg in arguments)
                {
                    code.AppendLine($"{arg.TypeName} arg_{arg.Name} = default!;");
                    code.AppendLine($"if (args.Length > {arg.Position})");
                    using (code.StartBlock())
                    {
                        switch (arg.TypeName)
                        {
                            case "int":
                            case "Int32":
                            case "short":
                            case "long":
                            case "Int64":
                            case "byte":
                            case "uint":
                            case "ulong":
                            case "ushort":
                                TryParseParserGenerator(code, arg);
                                break;
                            case "string":
                            default:
                                code.AppendLine($"arg_{arg.Name} = args[{arg.Position}];");
                                break;

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

                var options = command.Properties.OfType<OptionPropertyModel>().ToList();
                foreach (var opt in options)
                {
                    if (opt.IsCollection)
                    {
                        code.AppendLine($"List<{opt.ParseTypeName}> opt_{opt.Name} = [];");
                    }
                    else if (opt.TypeName == "bool")
                    {
                        code.AppendLine($"bool opt_{opt.Name} = false;");
                    }
                    else
                    {
                        code.AppendLine($"{opt.TypeName} opt_{opt.Name} = default!;");
                    }

                    code.AppendLine();
                }

                code.AppendLine("int state = 0;");
                code.AppendLine($"for (int i = {arguments.Count}; i < args.Length; i++)");
                using (code.StartBlock())
                {
                    code.AppendLine("switch (args[i])");
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
                                switch (opt.ParseTypeName)
                                {
                                    case "bool":
                                    case "int":
                                    case "Int32":
                                        using (code.StartBlock())
                                        {
                                            TryParseParserGenerator(code, opt);
                                        }
                                        break;
                                    case "string":
                                    default:
                                        if (opt.IsCollection)
                                        {
                                            code.AppendLine($"opt_{opt.Name}.Add(args[i]);");
                                        }
                                        else
                                        {
                                            code.AppendLine($"opt_{opt.Name} = args[i];");
                                        }
                                        break;
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
                code.AppendLine("StringBuilder messageBuilder = new StringBuilder();");
                code.AppendLine("messageBuilder.AppendLine(\"Failed to parse arguments:\");");
                code.AppendLine("foreach (string error in errors)");
                using (code.StartBlock())
                {
                    code.AppendLine("messageBuilder.AppendLine($\"\\t{error}\");");
                }
                code.AppendLine("throw new ArgumentException(messageBuilder.ToString());");
            }
        }

        context.AddSource($"{command.ClassName}Parser.g.cs", code.ToString());
    }

    private static void TryParseParserGenerator(CodeBuilder code, PropertyModel model)
    {
        switch (model)
        {
            case ArgumentPropertyModel argModel:
                code.AppendLine($"if (!{argModel.ParseTypeName}.TryParse(args[{argModel.Position}], out arg_{argModel.Name}))");
                using (code.StartBlock())
                {
                    code.AppendLine($"errors.Add(\"Argument {argModel.Name} can not be parsed to type: {argModel.ParseTypeName}\");");
                }
                break;
            case OptionPropertyModel optModel:
                code.AppendLine($"if ({optModel.ParseTypeName}.TryParse(args[i], out {optModel.ParseTypeName} v))");
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
                    code.AppendLine($"errors.Add($\"Invalid {optModel.ParseTypeName} value ({{args[i]}}) for option '--{optModel.OptionName}' at position {{i}}.\");");
                }
                break;
            default:
                break;
        }
    }

    #endregion
}

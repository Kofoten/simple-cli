using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace Kofoten.SimpleCli.Generator;

public record CommandModel(
    string Namespace,
    string ClassName,
    List<ConstructorParameterModel> ConstructorParameters,
    List<PropertyModel> Properties);

public record ConstructorParameterModel(
    string Name,
    string TypeName
);

public record PropertyModel(
    string Name,
    string TypeName,
    bool IsRequired,
    bool IsCollection,
    bool IsOption,
    int Position,
    string? OptionName,
    char? ShortName
);

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

            bool isCollection = false;
            if (member.Type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                isCollection = SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, enumerableSymbol);
            }

            if (argAttribute != null
                &&
                argAttribute.ConstructorArguments.Length > 0
                &&
                argAttribute.ConstructorArguments[0].Value is int position)
            {
                properties.Add(new PropertyModel(
                    Name: member.Name,
                    TypeName: member.Type.ToDisplayString(),
                    IsRequired: member.IsRequired,
                    IsCollection: isCollection,
                    IsOption: false,
                    Position: position,
                    OptionName: null,
                    ShortName: null
                ));
            }
            else if (optAttribute != null
                &&
                optAttribute.ConstructorArguments.Length > 0
                &&
                optAttribute.ConstructorArguments[0].Value is string optName)
            {
                var shortArg = optAttribute.NamedArguments.FirstOrDefault(na => na.Key == "Short");
                char? shortName = shortArg.Value.Value is char c && c != '\0' ? c : null;

                properties.Add(new PropertyModel(
                    Name: member.Name,
                    TypeName: member.Type.ToDisplayString(),
                    IsRequired: member.IsRequired,
                    IsCollection: isCollection,
                    IsOption: true,
                    Position: -1,
                    OptionName: optName,
                    ShortName: shortName
                ));
            }
        }

        return new CommandModel(
            Namespace: classSymbol.ContainingNamespace.ToDisplayString(),
            ClassName: classSymbol.Name,
            ConstructorParameters: constructorParams,
            Properties: properties
        );
    }

    private static void GenerateParser(SourceProductionContext context, CommandModel command)
    {
        var code = new CodeBuilder();
        code.AppendLine("// <auto-generated/>");
        code.AppendLine("#nullable enable");
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
                code.AppendLine("List<Exception> errors = [];");
                code.AppendLine();

                var arguments = command.Properties.Where(p => !p.IsOption).OrderBy(p => p.Position).ToList();
                foreach (var arg in arguments)
                {
                    code.AppendLine($"{arg.TypeName} arg_{arg.Name} = default!;");
                    code.AppendLine($"if (args.Length > {arg.Position})");
                    using (code.StartBlock())
                    {
                        switch (arg.TypeName)
                        {
                            case "int":
                                IntArgumentParserGenerator(code, arg);
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
                            code.AppendLine($"errors.Add(new ArgumentException(\"Missing required argument {arg.Name}\"));");
                        }
                    }

                    code.AppendLine();
                }

                var options = command.Properties.Where(p => p.IsOption).ToList();
                foreach (var opt in options)
                {
                    if (opt.IsCollection)
                    {
                        var innerType = opt.TypeName.Replace("System.Collections.Generic.IEnumerable<", "").TrimEnd('>');
                        code.AppendLine($"List<{innerType}> opt_{opt.Name} = [];");
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
                                if (opt.IsCollection)
                                {
                                    var innerType = opt.TypeName.Replace("System.Collections.Generic.IEnumerable<", "").TrimEnd('>');
                                    switch (innerType)
                                    {
                                        case "int":
                                            using (code.StartBlock())
                                            {
                                                IntCollectionParserGenerator(code, opt);
                                            }
                                            break;
                                        case "string":
                                        default:
                                            code.AppendLine($"opt_{opt.Name}.Add(args[i]);");
                                            break;
                                    }
                                }
                                else
                                {
                                    switch (opt.TypeName)
                                    {
                                        case "bool":
                                            using (code.StartBlock())
                                            {
                                                BoolOptionParserGenerator(code, opt);
                                            }
                                            break;
                                        case "int":
                                            using (code.StartBlock())
                                            {
                                                IntOptionParserGenerator(code, opt);
                                            }
                                            break;
                                        case "string":
                                        default:
                                            code.AppendLine($"opt_{opt.Name} = args[i];");
                                            break;
                                    }

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
                            string prefix = prop.IsOption ? "opt_" : "arg_";
                            code.AppendLine($"{prop.Name} = {prefix}{prop.Name},");
                        }
                    }
                    code.AppendLine("};");
                }

                code.AppendLine("throw new AggregateException(errors);");
            }
        }

        context.AddSource($"{command.ClassName}Parser.g.cs", code.ToString());
    }

    private static void IntArgumentParserGenerator(CodeBuilder code, PropertyModel model)
    {
        code.AppendLine($"if (!int.TryParse(args[{model.Position}], out arg_{model.Name}))");
        using (code.StartBlock())
        {
            code.AppendLine($"errors.Add(new ArgumentException(\"Argument {model.Name} is not an integer\"));");
        }
    }

    private static void IntOptionParserGenerator(CodeBuilder code, PropertyModel model)
    {
        code.AppendLine($"if (int.TryParse(args[i], out int v))");
        using (code.StartBlock())
        {
            code.AppendLine($"opt_{model.Name} = v;");
        }
        code.AppendLine("else");
        using (code.StartBlock())
        {
            code.AppendLine($"errors.Add(new ArgumentException($\"Invalid integer value ({{args[i]}}) for option '--{model.OptionName}' at position {{i}}.\"));");
        }
    }

    private static void IntCollectionParserGenerator(CodeBuilder code, PropertyModel model)
    {
        code.AppendLine($"if (int.TryParse(args[i], out int v))");
        using (code.StartBlock())
        {
            code.AppendLine($"opt_{model.Name}.Add(v);");
        }
        code.AppendLine("else");
        using (code.StartBlock())
        {
            code.AppendLine($"errors.Add(new ArgumentException($\"Invalid integer value ({{args[i]}}) for option '--{model.OptionName}' at position {{i}}.\"));");
        }
    }

    private static void BoolOptionParserGenerator(CodeBuilder code, PropertyModel model)
    {
        code.AppendLine($"if (bool.TryParse(args[i], out bool v))");
        using (code.StartBlock())
        {
            code.AppendLine($"opt_{model.Name} = v;");
        }
        code.AppendLine("else");
        using (code.StartBlock())
        {
            code.AppendLine($"errors.Add(new ArgumentException($\"Invalid boolean value ({{args[i]}}) for option '--{model.OptionName}' at position {{i}}.\"));");
        }
    }
}
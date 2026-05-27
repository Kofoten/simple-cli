using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace Kofoten.SimpleCli.Generator;

[Generator]
public class CliParsersSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var commandDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (s, _) => s is ClassDeclarationSyntax c && c.BaseList is not null,
                transform: (ctx, _) => GetCommandTarget(ctx))
            .Where(m => m is not null);
    }

    private static ClassDeclarationSyntax? GetCommandTarget(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        var inheritsCommand = classSymbol.AllInterfaces.Any(interfaceSymbol =>
            interfaceSymbol.ToDisplayString() == "Kofoten.SimpleCli.ICliCommand"
            ||
            interfaceSymbol.ToDisplayString() == "Kofoten.SimpleCli.IAsyncCliCommand");

        return inheritsCommand ? classDecl : null;
    }

    private void GenerateParser(SourceProductionContext context, INamedTypeSymbol classSymbol)
    {
    }
}

using DynamicModule.Exceptions;
using DynamicModule.Services.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace DynamicModule.Services;

public sealed class AnalyseCodeService : IAnalyseCodeService
{
    private readonly ILogger<AnalyseCodeService> _logger;
    private static readonly string[] _blackListedNamespaces = { "Microsoft.CodeAnalysis", "System.Reflection" };

    public AnalyseCodeService(ILogger<AnalyseCodeService> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task ValidateCodeAsync(CSharpCompilation cSharpCompilation, bool storeAnalysis, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Validating code for newly generated assembly {AssemblyName}.", cSharpCompilation.AssemblyName);

        foreach (var tree in cSharpCompilation.SyntaxTrees)
        {
            var semantic = cSharpCompilation.GetSemanticModel(tree, ignoreAccessibility: true);

            SyntaxNode classDeclarationSyntax = await tree.GetRootAsync(cancellationToken);

            ThrowIfBlacklistedNamespacesDetected(tree, semantic);
            foreach (ClassDeclarationSyntax classSymbol in classDeclarationSyntax.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>())
            {
                INamedTypeSymbol? s = semantic.GetDeclaredSymbol(classSymbol, cancellationToken: cancellationToken);
                // Properties
                var properties = GetProperties(s);

                static List<ISymbol> GetProperties(INamedTypeSymbol? symbol)
                {
                    List<ISymbol> result = new();
                    while (symbol != null)
                    {
                        result.AddRange(symbol.GetMembers().Where(m => m.Kind is SymbolKind.Property));
                        symbol = symbol.BaseType;
                    }

                    return result;
                }

                if (s is not null)
                {
                    foreach (var constructor in s.Constructors)
                    {
                        _logger.LogDebug("Assembly {AssemblyName} contains a Constructor {Name} with MetaData {MetaData}", cSharpCompilation.AssemblyName, constructor.Name, constructor.MetadataName);
                    }
                }
            }

            if (storeAnalysis)
            {
                await File.WriteAllTextAsync($"Analysis_{cSharpCompilation.AssemblyName}.txt", cSharpCompilation.AssemblyName, cancellationToken);
            }
        }
    }

    private static void ThrowIfBlacklistedNamespacesDetected(SyntaxTree syntaxTree, SemanticModel semanticModel)
    {
        CompilationUnitSyntax root = (CompilationUnitSyntax)syntaxTree.GetRoot();
        IEnumerable<IdentifierNameSyntax> identifiers = root.DescendantNodes()
            .Where(s => s is IdentifierNameSyntax)
            .Cast<IdentifierNameSyntax>();

        foreach (IdentifierNameSyntax identifier in identifiers)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(identifier);

            if (symbolInfo.Symbol is { })
            {
                if (symbolInfo.Symbol.Kind == SymbolKind.Namespace)
                {
                    if (_blackListedNamespaces.Any(ns => ns == symbolInfo.Symbol.ToDisplayString()))
                    {
                        throw new DynamicCodeException($"Declaration of namespace '{symbolInfo.Symbol.ToDisplayString()}' is not allowed.");
                    }
                }
                else if (symbolInfo.Symbol.Kind == SymbolKind.NamedType)
                {
                    if (_blackListedNamespaces.Any(ns => symbolInfo.Symbol.ToDisplayString().StartsWith(ns + ".", StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new DynamicCodeException($"Use of namespace '{identifier.Identifier.ValueText}' is not allowed.");
                    }
                }
            }
        }
    }
}

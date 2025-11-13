using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DKNet.EfCore.DtoGenerator.Analyzers;

/// <summary>
///     Analyzer that ensures types annotated with <c>GenerateDto</c> are declared <c>partial</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GenerateDtoPartialAnalyzer : DiagnosticAnalyzer
{
    #region Fields

    private const string Category = "Usage";

    private static readonly LocalizableString Description =
        "When a type is annotated with [GenerateDto], the type must be partial so the code generator can generate members.";

    private static readonly LocalizableString MessageFormat =
        "Type '{0}' has [GenerateDto] attribute but is not declared partial";

    private static readonly LocalizableString Title = "Types with [GenerateDto] should be partial";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        true,
        Description);

    #endregion

    #region Properties

    /// <summary>
    ///     Supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    #endregion

    #region Methods

    private static void AnalyzeRecordDeclaration(SyntaxNodeAnalysisContext context)
    {
        var decl = (RecordDeclarationSyntax)context.Node;

        if (HasGenerateDtoAttribute(decl.AttributeLists, context.SemanticModel, context.CancellationToken)
            && !HasPartialModifier(decl.Modifiers))
        {
            var nameToken = decl.Identifier;
            // Fallback: record positional records may have Identifier as token
            var diag = Diagnostic.Create(Rule, nameToken.GetLocation(), nameToken.Text);
            context.ReportDiagnostic(diag);
        }
    }

    private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var decl = (ClassDeclarationSyntax)context.Node;

        if (HasGenerateDtoAttribute(decl.AttributeLists, context.SemanticModel, context.CancellationToken)
            && !HasPartialModifier(decl.Modifiers))
        {
            var diag = Diagnostic.Create(Rule, decl.Identifier.GetLocation(), decl.Identifier.Text);
            context.ReportDiagnostic(diag);
        }
    }

    private static bool HasGenerateDtoAttribute(SyntaxList<AttributeListSyntax> attributeLists, SemanticModel model,
        CancellationToken ct)
    {
        foreach (var list in attributeLists)
        foreach (var attr in list.Attributes)
        {
            var info = model.GetSymbolInfo(attr, ct);
            var methodSymbol = info.Symbol as IMethodSymbol ?? info.CandidateSymbols.FirstOrDefault() as IMethodSymbol;
            var attributeType = methodSymbol?.ContainingType;
            if (attributeType == null) continue;

            // Accept either 'GenerateDto' or 'GenerateDtoAttribute'
            var name = attributeType.Name;
            if (string.Equals(name, "GenerateDto", StringComparison.Ordinal) ||
                string.Equals(name, "GenerateDtoAttribute", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool HasPartialModifier(SyntaxTokenList modifiers)
        => modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

    /// <inheritdoc />
    /// <summary>
    ///     Initializes analyzer actions for class and record declarations.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeRecordDeclaration, SyntaxKind.RecordDeclaration);
    }

    #endregion

    /// <summary>
    ///     Diagnostic id reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "DKN001";

    // Use simple string literals for LocalizableString (implicit conversion available)
}
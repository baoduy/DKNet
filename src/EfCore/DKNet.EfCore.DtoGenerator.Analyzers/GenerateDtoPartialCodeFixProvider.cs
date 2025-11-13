// ...existing code...

using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace DKNet.EfCore.DtoGenerator.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GenerateDtoPartialCodeFixProvider))]
[Shared]
public sealed class GenerateDtoPartialCodeFixProvider : CodeFixProvider
{
    #region Fields

    private const string Title = "Make type partial";

    #endregion

    #region Properties

    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(GenerateDtoPartialAnalyzer.DiagnosticId);

    #endregion

    #region Methods

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    private static SyntaxTokenList InsertPartialInModifiers(SyntaxTokenList modifiers, SyntaxToken partialToken)
    {
        // Preferred ordering: accessibility -> partial -> other modifiers
        var accessModifiers = new[]
        {
            SyntaxKind.PublicKeyword, SyntaxKind.InternalKeyword, SyntaxKind.ProtectedKeyword, SyntaxKind.PrivateKeyword
        };
        var insertAt = 0;
        for (var i = 0; i < modifiers.Count; i++)
            if (accessModifiers.Contains(modifiers[i].Kind()))
                insertAt = i + 1;

        var list = modifiers.ToList();
        if (list.Any(t => t.IsKind(SyntaxKind.PartialKeyword))) return modifiers;

        list.Insert(insertAt, partialToken);
        return SyntaxFactory.TokenList(list);
    }

    private async Task<Document> MakePartialAsync(Document document, TypeDeclarationSyntax typeDecl,
        CancellationToken cancellationToken)
    {
        var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        var newModifiers = InsertPartialInModifiers(typeDecl.Modifiers, partialToken);
        var newDecl = typeDecl.WithModifiers(newModifiers).WithAdditionalAnnotations(Formatter.Annotation);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(typeDecl, newDecl);
        return document.WithSyntaxRoot(newRoot);
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        // try to get the nearest type declaration (class, struct, or record)
        var typeDecl = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (typeDecl == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                cancellationToken => MakePartialAsync(context.Document, typeDecl, cancellationToken),
                Title),
            diagnostic);
    }

    #endregion
}
// ...existing code...
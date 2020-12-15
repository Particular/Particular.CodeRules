﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Particular.CodeRules.MustImplementIHandleMessages
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ImplementIHandleMessagesFixer)), Shared]

    public class ImplementIHandleMessagesFixer : CodeFixProvider
    {
        private const string title = "Implement IHandleMessages<T>";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.MustImplementIHandleMessages);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var messageType = diagnostic.Properties["MessageType"];
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var interfaceNameToken = root.FindToken(diagnosticSpan.Start);
            var baseTypeSyntax = interfaceNameToken.Parent.Parent as SimpleBaseTypeSyntax;

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
              CodeAction.Create(title, c =>
              ImplementIHandleMessages(context.Document, baseTypeSyntax, messageType, c), equivalenceKey: title), diagnostic);
        }

        private async Task<Document> ImplementIHandleMessages(Document document, SimpleBaseTypeSyntax baseType, string messageType, CancellationToken cancellationToken)
        {
            var classDeclaration = baseType.Ancestors().OfType<ClassDeclarationSyntax>().First();

            var insertCode = @"
public async Task Handle(" + messageType + @" message, IMessageHandlerContext context)
{
}";

            var newSyntaxTree = CSharpSyntaxTree.ParseText(insertCode, cancellationToken: cancellationToken);
            var newMethodDeclaration = (await newSyntaxTree.GetRootAsync(cancellationToken))
                .ChildNodes().OfType<MethodDeclarationSyntax>().First()
                .WithAdditionalAnnotations(Formatter.Annotation);

            var root = await document.GetSyntaxRootAsync(cancellationToken);

            var newClassDeclaration = classDeclaration.AddMembers(newMethodDeclaration);

            var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

            var newDocument = document.WithSyntaxRoot(newRoot);

            return newDocument;
        }
    }
}

﻿namespace Particular.CodeRules.AwaitOrCaptureTasks
{
    using System.Collections.Immutable;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AtLeastOneImplementationAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        ///     Gets the list of supported diagnostics for the analyzer.
        /// </summary>
        /// <value>
        ///     The supported diagnostics.
        /// </value>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DiagnosticDescriptors.AtLeastOneImplementation);

        /// <summary>
        ///     Initializes the specified analyzer on the <paramref name="context" />.
        /// </summary>
        /// <param name="context">The context.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
        {
            if(!(context.Node is ClassDeclarationSyntax classDeclaration))
            {
                return;
            }

            if(classDeclaration.BaseList == null)
            {
                return;
            }

            foreach(var childNode in classDeclaration.BaseList.ChildNodes())
            {
                if(childNode is BaseTypeSyntax baseTypeSyntax)
                {
                    if(BaseTypeIsHandlerSignature(baseTypeSyntax, out var messageIdentifier))
                    {
                        if(!HasImplementationDefined(classDeclaration, messageIdentifier))
                        {
                            var location = baseTypeSyntax.GetLocation();
                            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.AtLeastOneImplementation, location);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private static bool BaseTypeIsHandlerSignature(BaseTypeSyntax baseTypeSyntax, out string messageIdentifier)
        {
            messageIdentifier = null;

            var namePart = baseTypeSyntax.GetFirstToken();
            if (namePart == null)
            {
                return false;
            }

            if (namePart.Text != "IHandleMessages" && namePart.Text != "IAmStartedByMessages")
            {
                return false;
            }

            var lessThanToken = namePart.GetNextToken();
            if (lessThanToken == null || lessThanToken.Text != "<")
            {
                return false;
            }

            var tClassToken = lessThanToken.GetNextToken();
            if (tClassToken == null)
            {
                return false;
            }
            messageIdentifier = tClassToken.Text;

            var gtToken = tClassToken.GetNextToken();
            if (gtToken == null || gtToken.Text != ">")
            {
                return false;
            }

            return true;
        }

        private static bool HasImplementationDefined(ClassDeclarationSyntax classDeclaration, string messageIdentifier)
        {
            foreach (var member in classDeclaration.Members)
            {
                if(member is MethodDeclarationSyntax methodDeclaration)
                {
                    if (IsMethodAHandleMethod(methodDeclaration, messageIdentifier))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool IsMethodAHandleMethod(MethodDeclarationSyntax methodDeclaration, string messageIdentifier)
        {
            if (methodDeclaration.Identifier.Text != "Handle")
            {
                return false;
            }

            var paramList = methodDeclaration.ParameterList.ChildNodes().ToImmutableArray();
            if (paramList.Length != 2 && paramList.Length != 3)
            {
                return false;
            }

            if (!(paramList[0] is ParameterSyntax msgParam) || (msgParam.Type as IdentifierNameSyntax).Identifier.ValueText != messageIdentifier)
            {
                return false;
            }

            if (!(paramList[1] is ParameterSyntax contextParam) || (contextParam.Type as IdentifierNameSyntax).Identifier.ValueText != "IMessageHandlerContext")
            {
                return false;
            }

            if(paramList.Length == 3)
            {
                if (!(paramList[2] is ParameterSyntax cancellationToken) || (cancellationToken.Type as IdentifierNameSyntax).Identifier.ValueText != "CancellationToken")
                {
                    return false;
                }
            }

            return true;
        }
    }
}
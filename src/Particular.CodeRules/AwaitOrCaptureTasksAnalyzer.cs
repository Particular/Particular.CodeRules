﻿namespace Particular.CodeRules.AwaitOrCaptureTasks
{
    using System.Collections.Immutable;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AwaitOrCaptureTasksAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DiagnosticDescriptors.AwaitOrCaptureTasks);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.InvocationExpression);
        }

        static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node as InvocationExpressionSyntax;

            if (node?.Parent is ExpressionStatementSyntax)
            {
                var symbol = context.SemanticModel.GetSymbolInfo(node.Expression).Symbol;

                if (IsDroppedTask(symbol))
                {
                    var location = node.GetLocation();
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.AwaitOrCaptureTasks, location);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        static bool IsDroppedTask(ISymbol symbol)
        {
            if (symbol is IMethodSymbol methodSymbol)
            {
                return DerivesFromTask(methodSymbol.ReturnType);
            }

            if (symbol is ILocalSymbol localSymbol)
            {
                // Possibly a Func or delegate that returns a Task
                var namedType = localSymbol.Type as INamedTypeSymbol;
                if (namedType?.TypeKind == TypeKind.Delegate)
                {
                    var delegateInvoke = namedType.DelegateInvokeMethod;
                    var returnType = delegateInvoke.ReturnType;
                    return DerivesFromTask(returnType);
                }
            }

            return false;
        }

        static bool DerivesFromTask(ITypeSymbol symbol)
        {
            while (symbol != null)
            {
                if (symbol.ToString() == "System.Threading.Tasks.Task")
                {
                    return true;
                }

                symbol = symbol.BaseType;
            }

            return false;
        }
    }
}
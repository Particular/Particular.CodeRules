namespace NServiceBus.Core.Analyzer.Tests.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.Diagnostics;

    public abstract class CodeFixTestFixture<TAnalyzer, TCodeFix> : AnalyzerTestFixture<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        protected async Task Assert(
            string original,
            string expected,
            Action<IEnumerable<Diagnostic>, IEnumerable<Diagnostic>, string> assertFixDidNotIntroducedCompilerDiagnostics,
            Action<string, string> assertExpectedActual,
            CancellationToken cancellationToken = default)
        {
            var actual = await Fix(original, assertFixDidNotIntroducedCompilerDiagnostics, cancellationToken).ConfigureAwait(false);

            // normalize line endings, just in case
            actual = actual.Replace("\r\n", "\n");
            expected = expected.Replace("\r\n", "\n");

            assertExpectedActual(expected, actual);
        }

        static async Task<string> Fix(
            string code,
            Action<IEnumerable<Diagnostic>, IEnumerable<Diagnostic>, string> assertFixDidNotIntroducedCompilerDiagnostics,
            CancellationToken cancellationToken,
            IEnumerable<Diagnostic> originalCompilerDiagnostics = null)
        {
            WriteCode(code);

            var document = CreateDocument(code);

            var compilerDiagnostics = await document.GetCompilerDiagnostics(cancellationToken).ConfigureAwait(false);
            WriteCompilerDiagnostics(compilerDiagnostics);

            if (originalCompilerDiagnostics == null)
            {
                originalCompilerDiagnostics = compilerDiagnostics;
            }
            else
            {
                assertFixDidNotIntroducedCompilerDiagnostics(originalCompilerDiagnostics, compilerDiagnostics, "Fix introduced new compiler diagnostics.");
            }

            var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            compilation.Compile();

            var analyzerDiagnostics = (await compilation.GetAnalyzerDiagnostics(new TAnalyzer(), cancellationToken).ConfigureAwait(false)).ToList();
            WriteAnalyzerDiagnostics(analyzerDiagnostics);

            if (!analyzerDiagnostics.Any())
            {
                return code;
            }

            var actions = await document.GetCodeActions(new TCodeFix(), analyzerDiagnostics.First(), cancellationToken).ConfigureAwait(false);

            if (!actions.Any())
            {
                return code;
            }

            Console.WriteLine("Applying code fix actions...");
            foreach (var action in actions)
            {
                document = await document.ApplyChanges(action, cancellationToken).ConfigureAwait(false);
            }

            code = await document.GetCode(cancellationToken).ConfigureAwait(false);

            return await Fix(code, assertFixDidNotIntroducedCompilerDiagnostics, cancellationToken, originalCompilerDiagnostics).ConfigureAwait(false);
        }
    }
}

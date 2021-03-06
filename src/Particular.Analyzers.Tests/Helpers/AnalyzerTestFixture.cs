﻿namespace Particular.Analyzers.Tests.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Text;
    using Xunit.Abstractions;

    public class AnalyzerTestFixture<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
    {
        public AnalyzerTestFixture(ITestOutputHelper output) => Output = output;

        AnalyzerTestFixture() { }

        protected static readonly List<string> PrivateModifiers = new List<string> { "", "private" };

        protected static readonly List<string> NonPrivateModifiers = new List<string> { "public", "protected", "internal", "protected internal", "private protected" };

        protected static readonly List<string> InterfacePrivateModifiers = new List<string>
        {
#if NETCOREAPP
            "private",
#endif
        };

        protected static readonly List<string> InterfaceNonPrivateModifiers = new List<string>
        {
            "",
            "public",
            "internal",
#if NETCOREAPP
            "protected",
            "protected internal",
            "private protected",
#endif
        };

        protected ITestOutputHelper Output { get; }

        protected Task Assert(string markupCode, CompilationOptions compilationOptions = null, CancellationToken cancellationToken = default) =>
            Assert(markupCode, Array.Empty<string>(), compilationOptions, cancellationToken);

        protected Task Assert(string markupCode, string expectedDiagnosticId, CompilationOptions compilationOptions = null, CancellationToken cancellationToken = default) =>
            Assert(markupCode, new[] { expectedDiagnosticId }, compilationOptions, cancellationToken);

        protected async Task Assert(string markupCode, string[] expectedDiagnosticIds, CompilationOptions compilationOptions = null, CancellationToken cancellationToken = default)
        {
            var externalTypes =
@"namespace NServiceBus
{
    interface ICancellableContext { }
    class CancellableContext : ICancellableContext { }
    interface IMessage { }
}";

            markupCode =
@"#pragma warning disable CS8019
using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
#pragma warning restore CS8019

" +
                markupCode;

            var (code, markupSpans) = Parse(markupCode);
            WriteCode(Output, code);

            var document = CreateDocument(code, externalTypes, compilationOptions);

            var compilerDiagnostics = await document.GetCompilerDiagnostics(cancellationToken);
            WriteCompilerDiagnostics(Output, compilerDiagnostics);

            var compilation = await document.Project.GetCompilationAsync(cancellationToken);
            compilation.Compile();

            var analyzerDiagnostics = (await compilation.GetAnalyzerDiagnostics(new TAnalyzer(), cancellationToken)).ToList();
            WriteAnalyzerDiagnostics(Output, analyzerDiagnostics);

            var expectedSpansAndIds = expectedDiagnosticIds
                .SelectMany(id => markupSpans.Select(span => (span, id)))
                .OrderBy(item => item.span)
                .ThenBy(item => item.id)
                .ToList();

            var actualSpansAndIds = analyzerDiagnostics
                .Select(diagnostic => (diagnostic.Location.SourceSpan, diagnostic.Id))
                .ToList();

            Xunit.Assert.Equal(expectedSpansAndIds, actualSpansAndIds);
        }

        protected static void WriteCode(ITestOutputHelper Output, string code)
        {
            foreach (var (line, index) in code.Replace("\r\n", "\n").Split('\n')
                .Select((line, index) => (line, index)))
            {
                Output.WriteLine($"  {index + 1,3}: {line}");
            }
        }

        protected static Document CreateDocument(string code, string externalTypes, CompilationOptions compilationOptions)
        {
            var references = ImmutableList.Create(
                MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location));

            return new AdhocWorkspace()
                .AddProject("TestProject", LanguageNames.CSharp)
                .WithCompilationOptions(compilationOptions ?? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddMetadataReferences(references)
                .AddDocument("Externaltypes", externalTypes)
                .Project
                .AddDocument("TestDocument", code);
        }

        protected static void WriteCompilerDiagnostics(ITestOutputHelper Output, IEnumerable<Diagnostic> diagnostics)
        {
            Output.WriteLine("Compiler diagnostics:");

            foreach (var diagnostic in diagnostics)
            {
                Output.WriteLine($"  {diagnostic}");
            }
        }

        protected static void WriteAnalyzerDiagnostics(ITestOutputHelper Output, IEnumerable<Diagnostic> diagnostics)
        {
            Output.WriteLine("Analyzer diagnostics:");

            foreach (var diagnostic in diagnostics)
            {
                Output.WriteLine($"  {diagnostic}");
            }
        }

        static (string, List<TextSpan>) Parse(string markupCode)
        {
            if (markupCode == null)
            {
                return (null, new List<TextSpan>());
            }

            var code = new StringBuilder();
            var markupSpans = new List<TextSpan>();

            var remainingCode = markupCode;
            var remainingCodeStart = 0;

            while (remainingCode.Length > 0)
            {
                var beforeAndAfterOpening = remainingCode.Split(new[] { "[|" }, 2, StringSplitOptions.None);

                if (beforeAndAfterOpening.Length == 1)
                {
                    _ = code.Append(beforeAndAfterOpening[0]);
                    break;
                }

                var midAndAfterClosing = beforeAndAfterOpening[1].Split(new[] { "|]" }, 2, StringSplitOptions.None);

                if (midAndAfterClosing.Length == 1)
                {
                    throw new Exception("The markup code does not contain a closing '|]'");
                }

                var markupSpan = new TextSpan(remainingCodeStart + beforeAndAfterOpening[0].Length, midAndAfterClosing[0].Length);

                _ = code.Append(beforeAndAfterOpening[0]).Append(midAndAfterClosing[0]);
                markupSpans.Add(markupSpan);

                remainingCode = midAndAfterClosing[1];
                remainingCodeStart += beforeAndAfterOpening[0].Length + markupSpan.Length;
            }

            return (code.ToString(), markupSpans);
        }
    }
}

﻿namespace Particular.Analyzers.Tests.Cancellation
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Particular.Analyzers.Cancellation;
    using Particular.Analyzers.Tests.Helpers;
    using Xunit;
    using Xunit.Abstractions;
    using Data = System.Collections.Generic.IEnumerable<object[]>;

    public class TokenAccessibilityAnalyzerTests : AnalyzerTestFixture<TokenAccessibilityAnalyzer>
    {
        static readonly string method =
@"class MyClass
{{
    {0} void MyMethod({1}) {{ }}
}}";

        static readonly string @override =
@"class MyBase
{{
    {0} virtual void MyMethod({1}) {{ }}
}}

class MyClass : MyBase
{{    
    {0} override void MyMethod({1}) {{ }}
}}";

        static readonly string @explicit =
@"interface IMyInterface
{{
#pragma warning disable
    {0} void MyMethod({1});
#pragma warning restore
}}

class MyClass : IMyInterface
{{
    void IMyInterface.MyMethod({1}) {{ }}
}}";

        // delegate is nested to allow use of private modifier
        static readonly string @delegate =
@"class MyClass
{{
    {0} delegate void MyDelegate({1});
}}";

        static readonly string interfaceMethods =
@"interface IMyType
{{
    {0} void MyMethod({1});
}}";

#if NETCOREAPP
        static readonly string interfaceDefaultMethods =
@"interface IMyType
{{
    {0} void MyMethod({1}) {{ }}
}}";
#endif

        static readonly List<string> privateParams = new List<string>
        {
            "object foo, CancellationToken [|cancellationToken|]",
            "object foo, CancellationToken [|cancellationToken1|], CancellationToken [|cancellationToken2|]",
            "object foo, CancellationToken [|cancellationToken1|], CancellationToken [|cancellationToken2|], CancellationToken [|cancellationToken3|]",
        };

        static readonly List<string> nonPrivateParams = new List<string>
        {
            "object foo, CancellationToken [|cancellationToken|] = default",
            "object foo, CancellationToken [|cancellationToken1|] = default, CancellationToken [|cancellationToken2|] = default",
            "object foo, CancellationToken [|cancellationToken1|] = default, CancellationToken [|cancellationToken2|] = default, CancellationToken [|cancellationToken3|] = default",
        };

        public TokenAccessibilityAnalyzerTests(ITestOutputHelper output) : base(output) { }

        public static Data SadData =>
            PrivateModifiers.SelectMany(modifiers => nonPrivateParams.Select(param => (modifiers, param, DiagnosticIds.CancellationTokenPrivateOptional)))
            .Concat(NonPrivateModifiers.SelectMany(modifiers => privateParams.Select(param => (modifiers, param, DiagnosticIds.CancellationTokenNonPrivateRequired)))).ToData();

        public static Data HappyData =>
            PrivateModifiers.SelectMany(modifiers => privateParams.Select(param => (modifiers, param)))
            .Concat(NonPrivateModifiers.SelectMany(modifiers => nonPrivateParams.Select(param => (modifiers, param)))).ToData();

        public static Data SadOverridesData =>
            NonPrivateModifiers.SelectMany(modifiers => privateParams.Select(param => (modifiers, param, DiagnosticIds.CancellationTokenNonPrivateRequired))).ToData();

        public static Data HappyOverridesData =>
            NonPrivateModifiers.SelectMany(modifiers => nonPrivateParams.Select(param => (modifiers, param))).ToData();

        public static Data SadInterfaceMethodData =>
            InterfaceNonPrivateModifiers.SelectMany(modifiers => privateParams.Select(param => (modifiers, param, DiagnosticIds.CancellationTokenNonPrivateRequired))).ToData();

        public static Data HappyInterfaceMethodData =>
            InterfaceNonPrivateModifiers.SelectMany(modifiers => nonPrivateParams.Select(param => (modifiers, param))).ToData();

        public static Data SadInterfaceDefaultMethodData =>
            InterfacePrivateModifiers.SelectMany(modifiers => nonPrivateParams.Select(param => (modifiers, param, DiagnosticIds.CancellationTokenPrivateOptional)))
            .Concat(InterfaceNonPrivateModifiers.SelectMany(modifiers => privateParams.Select(param => (modifiers, param, DiagnosticIds.CancellationTokenNonPrivateRequired)))).ToData();

        public static Data HappyInterfaceDefaultMethodData =>
            InterfacePrivateModifiers.SelectMany(modifiers => privateParams.Select(param => (modifiers, param)))
            .Concat(InterfaceNonPrivateModifiers.SelectMany(modifiers => nonPrivateParams.Select(param => (modifiers, param)))).ToData();

        [Theory]
        [MemberData(nameof(SadData))]
        public Task SadMethods(string modifiers, string @params, string diagnosticId) => Assert(GetCode(method, modifiers, @params), diagnosticId);

        [Theory]
        [MemberData(nameof(HappyData))]
        public Task HappyMethods(string modifiers, string @params) => Assert(GetCode(method, modifiers, @params));

        [Theory]
        [MemberData(nameof(SadOverridesData))]
        public Task SadOverrides(string modifiers, string @params, string diagnosticId) => Assert(GetCode(@override, modifiers, @params), diagnosticId);

        [Theory]
        [MemberData(nameof(HappyOverridesData))]
        public Task HappyOverrides(string modifiers, string @params) => Assert(GetCode(@override, modifiers, @params));

        [Theory]
        [MemberData(nameof(SadInterfaceMethodData))]
        [MemberData(nameof(HappyInterfaceMethodData))]
        public Task HappyExplicits(string modifiers, string @params, params string[] diagnosticIds) => Assert(RemoveMarkUp(GetCode(@explicit, modifiers, @params)), diagnosticIds);

        [Theory]
        [MemberData(nameof(SadData))]
        public Task SadDelegates(string modifiers, string @params, string diagnosticId) => Assert(GetCode(@delegate, modifiers, @params), diagnosticId);

        [Theory]
        [MemberData(nameof(HappyData))]
        public Task HappyDelegates(string modifiers, string @params) => Assert(GetCode(@delegate, modifiers, @params));

        [Theory]
        [MemberData(nameof(SadInterfaceMethodData))]
        public Task SadInterfaceMethods(string modifiers, string @params, string diagnosticId) => Assert(GetCode(interfaceMethods, modifiers, @params), diagnosticId);

        [Theory]
        [MemberData(nameof(HappyInterfaceMethodData))]
        public Task HappyInterfaceMethods(string modifiers, string @params) => Assert(GetCode(interfaceMethods, modifiers, @params));

#if NETCOREAPP
        [Theory]
        [MemberData(nameof(SadInterfaceDefaultMethodData))]
        public Task SadInterfaceDefaultMethods(string modifiers, string @params, string diagnosticId) => Assert(GetCode(interfaceDefaultMethods, modifiers, @params), diagnosticId);

        [Theory]
        [MemberData(nameof(HappyInterfaceDefaultMethodData))]
        public Task HappyInterfaceDefaultMethods(string modifiers, string @params) => Assert(GetCode(interfaceDefaultMethods, modifiers, @params));
#endif

        static string GetCode(string template, string modifiers, string @params) => string.Format(template, modifiers, @params);

        static string RemoveMarkUp(string code) => code.Replace("[|", "").Replace("|]", "");
    }
}

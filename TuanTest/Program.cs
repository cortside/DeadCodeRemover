using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.Diagnostics.SymbolStore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace TuanTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var visualStudioInstances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            var instance = visualStudioInstances[0];
            MSBuildLocator.RegisterInstance(instance);
            using var workspace = MSBuildWorkspace.Create();
            //var solution = await workspace.OpenSolutionAsync(@"D:\wWw\var\QuangMccbs\QuangMccbs.sln");
            //var project = solution.Projects.First();
            var project = await workspace.OpenProjectAsync(@"D:\wWw\var\QuangMccbs\Src\QuangMccbs.csproj");
            foreach (var document in project.Documents)
            {
                Console.WriteLine(document.FilePath);

                var model = await document.GetSemanticModelAsync() ?? throw new InvalidOperationException();
                var syntaxNode = await document.GetSyntaxRootAsync() ?? throw new InvalidOperationException();
                var methodInvocations = syntaxNode.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in methodInvocations)
                {
                    Console.WriteLine(invocation.ToFullString());
                    var symbolInfo = model.GetSymbolInfo(invocation);
                    var methodSymbol = symbolInfo.Symbol;
                    if(methodSymbol is null) continue;

                    var references = await SymbolFinder.FindReferencesAsync(methodSymbol, document.Project.Solution);

                    foreach (var reference in references)
                    {
                        Console.WriteLine(reference.Definition.ToDisplayString());
                    }
                }
            }
        }
    }
}

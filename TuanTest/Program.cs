using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.Diagnostics.SymbolStore;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Document = Microsoft.CodeAnalysis.Document;
using Microsoft.Build.Evaluation;
using Project = Microsoft.CodeAnalysis.Project;

namespace TuanTest
{
    internal class Program
    {
        private static async Task<Project> SaveChanges(DocumentEditor editor)
        {
            var changedDocument = editor.GetChangedDocument();
            var syntaxNode = await changedDocument.GetSyntaxRootAsync();
            if (syntaxNode != null)
            {
                var declarations = syntaxNode.DescendantNodes();

                if (!declarations.Any(d =>
                        d.IsKind(SyntaxKind.ClassDeclaration) ||
                        d.IsKind(SyntaxKind.StructDeclaration) ||
                        d.IsKind(SyntaxKind.InterfaceDeclaration) ||
                        d.IsKind(SyntaxKind.EnumDeclaration)))
                {
                    return editor.OriginalDocument.Project.RemoveDocument(editor.OriginalDocument.Id);
                }
            }

            var newContent = (await changedDocument.GetSyntaxTreeAsync() ?? throw new InvalidOperationException())
                .GetCompilationUnitRoot()
                //.NormalizeWhitespace()
                .GetText()
                .ToString();
            
            var path = changedDocument.FilePath ?? throw new InvalidOperationException();
            using var fs = new StreamWriter(path);
            await fs.WriteAsync(newContent);
            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="project"></param>
        /// <param name="metaName">QuangMccbs.Classes.Class144</param>
        /// <param name="methodName">method_7</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static async Task<IMethodSymbol> GetMethod(Project project, string metaName, string methodName)
        {
            var compilation = await project.GetCompilationAsync() ?? throw new InvalidOperationException();
            var getClass = compilation.GetTypeByMetadataName(metaName) ?? throw new InvalidOperationException();
            var getMethod = getClass.GetMembers(methodName).First() as IMethodSymbol ?? throw new InvalidOperationException();
            return getMethod;
        }

        private static async Task<int> GetMethodRefCount(ISymbol symbol, Solution solution)
        {
            var references = await SymbolFinder.FindReferencesAsync(symbol, solution);
            //var callers = await SymbolFinder.FindCallersAsync(method, project.Solution);
            var count = references.Select(x => x.Locations.Count()).Sum();
            return count;
        }

        // ReSharper disable once UnusedMember.Local
        private static async Task<int> GetMethodRefCount(Project project, string metaName, string methodName)
        {
            var method = await GetMethod(project, metaName, methodName);
            return await GetMethodRefCount(method, project.Solution);
        }

        private static async Task<string> Clean(Project project)
        {
            var sb = new StringBuilder();
            foreach (var document in project.Documents)
            {
                sb.AppendLine($"#{document.Name}");
                if(document.Name.Equals("Class144.cs")) continue;

                var model = await document.GetSemanticModelAsync() ?? throw new InvalidOperationException();
                var root = await document.GetSyntaxRootAsync() ?? throw new InvalidOperationException();
                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var @class in classes)
                {
                    sb.AppendLine($"##{@class.Identifier.ValueText}");
                    if (@class.Identifier.ValueText.Equals("Class144")) continue;

                    var methods = @class.DescendantNodes().OfType<MethodDeclarationSyntax>();
                    foreach (var method in methods)
                    {
                        //var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();

                        sb.AppendLine($"###{method.Identifier.ValueText}");
                        if (method.Identifier.ValueText.Equals("method_7")) continue;

                        var symbolInfo = model.GetSymbolInfo(method);
                        var methodSymbol = symbolInfo.Symbol;
                        if (methodSymbol is null) continue;
                        var count = await GetMethodRefCount(methodSymbol, project.Solution);

                        sb.AppendLine($"####count:{count}{(count <= 0 ? "->CLEAN" : "")}");
                        
                        if (count >0) continue;

                        //var editor = await DocumentEditor.CreateAsync(document);
                        //editor.RemoveNode(method);
                        //await SaveChanges(editor);
                    }

                    var fields = @class.DescendantNodes().OfType<FieldDeclarationSyntax>();
                    foreach (var field in fields)
                    {
                        sb.AppendLine($"###{field.Declaration.Variables.First().Identifier.ValueText}");
                        if (field.Declaration.Variables.First().Identifier.ValueText.Equals("string_7")) continue;
                    }
                }
            }

            return sb.ToString();
        }

        private static async Task<IEnumerable<Project>> GetProjects(string path)
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances();
            MSBuildLocator.RegisterInstance(instances.First());
            
            var workspace = MSBuildWorkspace.Create();
            //workspace.Dispose(); using

            if (path.EndsWith(".sln"))
            {
                var solution = await workspace.OpenSolutionAsync(path);
                return solution.Projects;
            }

            if (!path.EndsWith(".csproj")) return null;
            var project = await workspace.OpenProjectAsync(path);
            return new List<Project> { project };
        }

        private static async Task Main(string[] args)
        {
            //const string path = @"D:\wWw\var\QuangMccbs\QuangMccbs.sln"
            const string path = @"D:\wWw\var\QuangMccbs\Src\QuangMccbs.csproj";
            var projects = await GetProjects(path);
            foreach (var project in projects)
            {
                var ret = await Clean(project);
                Console.WriteLine(ret);
            }
        }
    }
}
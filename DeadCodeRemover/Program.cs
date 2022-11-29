
using CommandLine;
using CsvHelper;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Globalization;

namespace DeadCodeRemover
{
    internal class Options
    {
        [Option('s', "solution", Required = true, HelpText = "Solution file to be processed.")]
        public string Solution { get; set; }
        [Option('p', "project", Required = false, HelpText = "Project file to be processed.")]
        public string Project { get; set; }
        [Option(Default = false, HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }
    }
    internal class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<Options>(args).WithParsedAsync(RunWithOptions);
            Console.ReadKey();
        }
        private static async Task RunWithOptions(Options opt)
        {
            var visualStudioInstances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            var instance = visualStudioInstances[0];
            var knowTypes = new KnownTypesRepository();
            knowTypes.LoadKnownTypes(Path.GetDirectoryName(opt.Solution) ?? throw new InvalidOperationException());
            MSBuildLocator.RegisterInstance(instance);
            using var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += (o, e) => Logger.Warn(e.Diagnostic.Message);
            Logger.Info($"Loading solution {opt.Solution} into workspace.");
            var solution = await workspace.OpenSolutionAsync(opt.Solution, new ConsoleProgressReporter());
            var typeBuilder = new TypeBuilder(Logger);
            var project = solution.Projects.FirstOrDefault(p => p.FilePath == opt.Project);
            if (project == null) throw new ArgumentException($"Cannot find project {opt.Project} in solution,");

            var types = await typeBuilder.BuildTypes(solution, new List<Project> { project }, knowTypes);
            var deadTypeRemover = new DeadTypeRemover(Logger);
            var typeInfos = types as TypeInfo[] ?? types.ToArray();
            await deadTypeRemover.RemoveDeadTypes(workspace, typeInfos.Where(t => t.IsDead == true));
            OutputResults(typeInfos);
            Console.ReadKey();
        }
        private static void OutputResults(IEnumerable<TypeInfo> types)
        {
            using var writer = new StreamWriter($"DeadCodeResult_Roslyn.csv");
            using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csvWriter.WriteField("Full Name");
            csvWriter.WriteField("Depth");
            csvWriter.WriteField("Project");
            csvWriter.WriteField("Source");
            csvWriter.WriteField("NumberOfLines");
            csvWriter.WriteField("Declaration Type");
            csvWriter.WriteField("Action");
            csvWriter.NextRecord();
            foreach (var type in types)
            {
                csvWriter.WriteField($"{type.FullName}");
                csvWriter.WriteField($"{type.Depth}");
                csvWriter.WriteField($"{type.ContainingProject.FilePath}");
                csvWriter.WriteField($"{type.ContainingDocument.FilePath}");
                csvWriter.WriteField($"{type.NumberOfLines}");
                csvWriter.WriteField($"{type.Symbol.TypeKind}");
                csvWriter.WriteField($"{type.RemovalAction}");
                csvWriter.NextRecord();
            }
        }

        // ReSharper disable once UnusedMember.Local
        private static VisualStudioInstance SelectVisualStudioInstance(IReadOnlyList<VisualStudioInstance> visualStudioInstances)
        {
            Logger.Info("Multiple installs of MSBuild detected please select one:");
            for (var i = 0; i < visualStudioInstances.Count; i++)
            {
                Logger.Info($"Instance {i + 1}");
                Logger.Info($" Name: {visualStudioInstances[i].Name}");
                Logger.Info($" Version: {visualStudioInstances[i].Version}");
                Logger.Info($" MSBuild Path: {visualStudioInstances[i].MSBuildPath}");
            }
            while (true)
            {
                var userResponse = Console.ReadLine();
                if (int.TryParse(userResponse, out var instanceNumber) &&
                instanceNumber > 0 &&
                instanceNumber <= visualStudioInstances.Count)
                {
                    return visualStudioInstances[instanceNumber - 1];
                }
                Logger.Error("Input not accepted, try again.");
            }
        }
        private class ConsoleProgressReporter : IProgress<ProjectLoadProgress>
        {
            public void Report(ProjectLoadProgress loadProgress)
            {
                var projectDisplay = Path.GetFileName(loadProgress.FilePath);
                if (loadProgress.TargetFramework != null)
                {
                    projectDisplay += $" ({loadProgress.TargetFramework})";
                }
                Logger.Debug($"{loadProgress.Operation,-15} {loadProgress.ElapsedTime,-15:m\\:ss\\.fffffff} {projectDisplay}");
            }
        }
    }
}
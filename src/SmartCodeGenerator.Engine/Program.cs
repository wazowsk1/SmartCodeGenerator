using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartCodeGenerator.Engine
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                Console.WriteLine(((Exception)eventArgs.ExceptionObject).ToString());
            };
            var progressReporter = new ProgressReporter();

            var options = OptionsHelper.LoadOptions(args, progressReporter);
            if (options == null)
            {
                return -1;
            }

            var selectedMsBuildInstance = MsBuildHelper.GetMsBuildInstance(progressReporter);
            if (selectedMsBuildInstance == null)
            {
                return -1;
            }

            MSBuildLocator.RegisterInstance(selectedMsBuildInstance);
            using (var workspace = MsBuildHelper.CreateMsBuildWorkspace(progressReporter))
            {
                var project = await workspace.OpenProjectAsync(options.ProjectPath, progressReporter);

                project = AddCompileDocuments(project, options.ProjectPath, options.CompilePaths);

                var generatorAssemblySearchPaths = options.GetGeneratorPluginsSearchPaths(progressReporter);
                var generator = new CompilationGenerator(generatorAssemblySearchPaths, options.OutputPath, progressReporter);
                await generator.Process(project);
            }
            return 0;
        }

        private static Project AddCompileDocuments(Project project, string projectPath, string compilePaths)
        {
            if (compilePaths == null)
            {
                return project;
            }

            var projectDirectoryPath = Path.GetDirectoryName(projectPath);
            foreach (var file in compilePaths.Split(";"))
            {
                var filePath = Path.IsPathFullyQualified(file) ? file : Path.Combine(projectDirectoryPath, file);
                if (!File.Exists(filePath))
                {
                    continue;
                }

                var name = Path.GetFileName(filePath);
                if (!project.Documents.Any(x => x.Name == name))
                {
                    var textDocument = project.AddDocument(name, File.ReadAllText(filePath), filePath: filePath);
                    project = textDocument.Project;
                }
            }

            return project;
        }
    }
}
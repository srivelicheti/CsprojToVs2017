using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Project2015To2017.Definition;
using Project2015To2017.Logging;
using Project2015To2017.Writing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;

[assembly: InternalsVisibleTo("Project2015To2017Tests")]

namespace Project2015To2017
{
    class Program
    {
        public static IConfigurationRoot Configuration { get; set; }
        public static ILogger<Program> logger;
        private static ILoggerFactory loggerFactory;
        private const string LoggerRepo = "AppLoggers";
        private static TransformationSettings transformationSettings;
        private static HashSet<string> processedFiles = new HashSet<string>();
        static List<string> ProcessImports(XDocument projectFile, DirectoryInfo projectDirectory, out List<XElement> importedXElements)
        {
            importedXElements = new List<XElement>();
            var paths = new List<string>();
            var imports = projectFile.Descendants().Where(x => x.Name.LocalName == "Import").ToList();
            foreach (var im in imports)
            {
                
                var relativePath = im.Attribute("Project")?.Value;
                if (relativePath != null &&
                    !relativePath.EndsWith("nuget.targets", StringComparison.OrdinalIgnoreCase) &&
                    !relativePath.EndsWith("csharp.targets", StringComparison.OrdinalIgnoreCase))
                {
                    importedXElements.Add(im);
                }

                if (string.IsNullOrEmpty(relativePath) || !relativePath.EndsWith("proj")) continue;

                string finalPath = string.Empty;

                if (System.IO.Path.IsPathRooted(relativePath))
                {
                    finalPath = relativePath;
                }
                else
                {
                   try{ finalPath = Path.Combine(projectDirectory.ToString(), relativePath); } catch { }
                }
                //process only relative paths
               if(string.IsNullOrEmpty(finalPath)) continue;
                paths.Add(finalPath);
            }

            return paths;
        }

        private static IReadOnlyList<ITransformation> _transformationsToApply;

        static void Main(string[] args)
        {
            //if (args.Length == 0)
            //{
            //    Console.WriteLine($"Please specify a project file.");
            //    return;
            //}

            StartUp();

            _transformationsToApply = new ITransformation[]
        {
            new ProjectPropertiesTransformation(loggerFactory),
            new ProjectReferenceTransformation(loggerFactory),
            new PackageReferenceTransformation(loggerFactory),
            new AssemblyReferenceTransformation(loggerFactory),
            new RemovePackageAssemblyReferencesTransformation(loggerFactory),
            new FileTransformation(loggerFactory),
            new AssemblyInfoTransformation(loggerFactory),
            new NugetPackageTransformation(loggerFactory)
        };

            var parser = new CommandLine.Parser(with => with.HelpWriter = Console.Error);

            var parseResult = parser.ParseArguments(() => transformationSettings, args);
            if (parseResult.Tag == CommandLine.ParserResultType.Parsed)
            {
                parseResult.WithParsed(x => transformationSettings = x);
                // Process all csprojs found in given directory
                if (Path.GetExtension(transformationSettings.Path) != ".csproj")
                {
                    var projectFiles = Directory.EnumerateFiles(transformationSettings.Path, "*.csproj", SearchOption.AllDirectories).ToList();
                    projectFiles.AddRange(Directory.EnumerateFiles(transformationSettings.Path, "*.vbproj",
                        SearchOption.AllDirectories));

                    if (projectFiles.Count == 0)
                    {
                        logger.LogError("No Project files found in the specified directory");
                        return;
                    }
                    logger.LogInformation($"Multiple project files found under directory {transformationSettings.Path}:");
                    logger.LogInformation(string.Join(Environment.NewLine, projectFiles));
                    foreach (var projectFile in projectFiles)
                    {
                        try
                        {
                            ProcessFile(projectFile);
                        }
                        catch(Exception ex)
                        {
                            logger.LogError($"unable to convert {projectFile}: ", ex);
                        }
                    }
                }
                else // Process only the given project file
                {
                    ProcessFile(transformationSettings.Path);
                }
            }
            else
            {
                logger.LogError("Parsing of command line options failed");
            }
            Console.WriteLine("Press Any Key to exit");
            Console.ReadKey();
        }

        private static void ProcessFile(string filePath, bool includeFileTranformations = true)
        {
            var file = new FileInfo(filePath);
            var fileInfo = new FileInfo(filePath);
            if (!Validate(file))
            {
                return;
            }

            if (!SaveBackup(fileInfo.FullName))
            {
                return;
            }
            logger.LogInformation($"Processing file {filePath}");
            XDocument xmlDocument;
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                xmlDocument = XDocument.Load(stream);
            }

            XNamespace nsSys = "http://schemas.microsoft.com/developer/msbuild/2003";
            if (xmlDocument.Element(nsSys + "Project") == null)
            {
                logger.LogError($"This is not a VS2015 project file.");
                return;
            }


            var projectDefinition = new Project() { ProjFilePath = Path.GetFullPath(filePath) };

            
            var directory = fileInfo.Directory;

            
            List<XElement> importedXElements;
            var projectImports = ProcessImports(xmlDocument, file.Directory, out importedXElements);
            projectDefinition.ImportedXElements = importedXElements;
            var importedProjects = new List<XDocument>();
            foreach (var projectImport in projectImports)
            {
                importedProjects.Add(XDocument.Load(projectImport));
                if(!processedFiles.Contains(Path.GetFullPath(projectImport)))
                    ProcessFile(projectImport,false);
            }

            projectDefinition.ProjectImports = importedProjects.ToArray();
            if(!includeFileTranformations)
                _transformationsToApply.Where(x => !typeof(FileTransformation).IsAssignableFrom(x.GetType())).Select(t => t.TransformAsync(true, xmlDocument, directory, projectDefinition))
                .ToArray();
            else
            Task.WaitAll(_transformationsToApply.Select(t => t.TransformAsync(true, xmlDocument, directory, projectDefinition))
                .ToArray());

            AssemblyReferenceTransformation.RemoveExtraAssemblyReferences(projectDefinition);

            var projectFile = fileInfo.FullName;
           

            var packagesFile = Path.Combine(fileInfo.DirectoryName, "packages.config");
            if (File.Exists(packagesFile))
            {
                if (!RenameFile(packagesFile))
                {
                    return;
                }
            }

            new ProjectWriter().Write(projectDefinition, fileInfo);
            processedFiles.Add(Path.GetFullPath(filePath));
        }

        internal static bool Validate(FileInfo file)
        {
            if (!file.Exists)
            {
                logger.LogWarning($"File {file.FullName} could not be found.");
                return false;
            }

            if (file.IsReadOnly)
            {
                logger.LogError($"File {file.FullName} is readonly, please make the file writable first (checkout from source control?).");
                return false;
            }

            return true;
        }

        private static bool SaveBackup(string filename)
        {
            var output = false;

            var backupFileName = filename + ".old";
            if (File.Exists(backupFileName))
            {
                return false;
                Console.Write($"Cannot create backup file. Please delete {backupFileName}. Do you want to Copy Old to Original csproj file y/N?");
                 if(Console.ReadLine().ToLowerInvariant() == "y")
                {
                    File.Delete(filename);
                    File.Copy(backupFileName, filename);
                 //   File.Copy(filename, filename + ".old");
                    output = true;
                }

            }
            else
            {
                File.Copy(filename, filename + ".old");
                output = true;
            }

            return output;
        }

        private static bool RenameFile(string filename)
        {
            var output = false;

            var backupFileName = filename + ".old";
            if (File.Exists(backupFileName))
            {
                Console.Write($"Cannot create backup file. Please delete {backupFileName}.");
            }
            else
            {
                // todo Consider using TF VC or Git?
                File.Move(filename, filename + ".old");
                output = true;
            }

            return output;
        }


        private static void StartUp()
        {
            var appRoot = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var builder = new ConfigurationBuilder()
                .SetBasePath(appRoot)
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables();

            var config = builder.Build();

            //var services = new ServiceCollection()
            //  .AddLogging();

            //var serviceProvider = services.BuildServiceProvider();

            Log4NetHelper.ConfigureLog4Net(appRoot, "Log4net.config", LoggerRepo);
            loggerFactory = new LoggerFactory().
                AddConsole(config.GetSection("Logging"))
                .AddDebug()
                 .AddLog4Net(LoggerRepo);

            logger = loggerFactory.CreateLogger<Program>();

            Configuration = config;
            transformationSettings = config.Get<TransformationSettings>();
                

           
        }

    }
}

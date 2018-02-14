using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Project2015To2017.Definition;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Project2015To2017
{
    internal sealed class AssemblyInfoTransformation : ITransformation
    {
        private readonly TransformationSettings transformationSettings;

        private ILogger Logger { get; set; }
        public AssemblyInfoTransformation(ILoggerFactory loggerFactory, TransformationSettings transformationSettings)
        {
            this.Logger = loggerFactory.CreateLogger<AssemblyInfoTransformation>();
            this.transformationSettings = transformationSettings;
        }
        public async Task<bool> TransformAsync(bool prevTransformationResult, XDocument projectFile, DirectoryInfo projectFolder, Project definition)
        {
            var assemblyInfoFiles = projectFolder
                .EnumerateFiles("*.AssemblyInfo.cs", SearchOption.AllDirectories)
                .ToList();

            assemblyInfoFiles.AddRange(projectFolder
                   .EnumerateFiles("*.AssemblyInfo.vb", SearchOption.AllDirectories));

            if (definition.ItemsToInclude.Count > 0)
            {
                //look for imported assemblyinfo.cs\vb files from parent
                var sharedIncludePaths = definition.ItemsToInclude.Select(x => x.Attribute("Include")?.Value).Where(x => !string.IsNullOrEmpty(x)).ToList();

                var sharedAssemblyInfos = sharedIncludePaths.Where(x => x.EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase) || x.EndsWith("AssemblyInfo.vb", StringComparison.OrdinalIgnoreCase))
                     .Select(x =>  new FileInfo(Path.GetFullPath(Path.Combine(projectFolder.FullName, x))));
                assemblyInfoFiles.AddRange(sharedAssemblyInfos);
            }

            if (assemblyInfoFiles.Count > 0)
            {
                Logger.LogDebug($"Reading assembly info from {assemblyInfoFiles[0].FullName}.");

                definition.AssemblyAttributes = new AssemblyAttributes
                {
                    AssemblyName = definition.AssemblyName ?? projectFolder.Name
                };

                foreach (var assemblyInfoFile in assemblyInfoFiles)
                {
                    string text;
                    using (var filestream = File.Open(assemblyInfoFile.FullName, FileMode.Open, FileAccess.Read))
                    using (var streamReader = new StreamReader(filestream))
                    {
                        text = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                    }

                    definition.AssemblyAttributes.Description = definition.AssemblyAttributes.Description ??
                        GetAttributeValue<AssemblyDescriptionAttribute>(text);
                    definition.AssemblyAttributes.Title = definition.AssemblyAttributes.Title ??
                        GetAttributeValue<AssemblyTitleAttribute>(text);
                    definition.AssemblyAttributes.Company = definition.AssemblyAttributes.Company ??
                        GetAttributeValue<AssemblyCompanyAttribute>(text);
                    definition.AssemblyAttributes.Product = definition.AssemblyAttributes.Product ??
                        GetAttributeValue<AssemblyProductAttribute>(text);
                    definition.AssemblyAttributes.Copyright = definition.AssemblyAttributes.Copyright ??
                        GetAttributeValue<AssemblyCopyrightAttribute>(text);
                    definition.AssemblyAttributes.InformationalVersion = definition.AssemblyAttributes.InformationalVersion ??
                        GetAttributeValue<AssemblyInformationalVersionAttribute>(text);
                    definition.AssemblyAttributes.Version = definition.AssemblyAttributes.Version ??
                        GetAttributeValue<AssemblyVersionAttribute>(text);
                    definition.AssemblyAttributes.FileVersion = definition.AssemblyAttributes.FileVersion ??
                        GetAttributeValue<AssemblyFileVersionAttribute>(text);
                    definition.AssemblyAttributes.Configuration = definition.AssemblyAttributes.Configuration ??
                        GetAttributeValue<AssemblyConfigurationAttribute>(text);
                }


            }
            else
            {
                Logger.LogInformation($@"Could not read from assemblyinfo, No assemblyinfo files found");
            }
            return (true);
        }

        private string GetAttributeValue<T>(string text)
            where T : Attribute
        {
            var attributeTypeName = typeof(T).Name;
            var attributeName = attributeTypeName.Substring(0, attributeTypeName.Length - 9);

            //var regex = new Regex($@"\[assembly:.*{attributeName}\(\""(?<value>.*)\""\)]", RegexOptions.Compiled);
            var regex = new Regex($@"\[assembly:.*{attributeName}\(\""(?<value>.*)\""\)]", RegexOptions.Compiled);

            // TODO parse this in roslyn so we actually know that it's not comments.
            var match = regex.Match(text);
            if (match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            regex = new Regex($@"\[assembly:.*{attributeTypeName}\(\""(?<value>.*)\""\)]", RegexOptions.Compiled);

            // TODO parse this in roslyn so we actually know that it's not comments.
            match = regex.Match(text);
            if (match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            return null;
        }
    }
}
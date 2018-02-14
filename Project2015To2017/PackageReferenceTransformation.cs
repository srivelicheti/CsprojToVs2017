using Project2015To2017.Definition;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Project2015To2017
{
    internal sealed class PackageReferenceTransformation : ITransformation
    {
        private readonly TransformationSettings transformationSettings;

        private ILogger Logger { get; set; }
        public PackageReferenceTransformation(ILoggerFactory loggerFactory, TransformationSettings transformationSettings)
        {
            this.Logger = loggerFactory.CreateLogger<PackageReferenceTransformation>();
            this.transformationSettings = transformationSettings;
        }
        public Task<bool> TransformAsync(bool prevTransformationResult, XDocument projectFile, DirectoryInfo projectFolder, Project definition)
        {
            //if (definition.Type == ApplicationType.Unknown) return Task.FromResult(false);
            var packagesConfig = projectFolder.GetFiles("packages.config", SearchOption.TopDirectoryOnly);
            if (packagesConfig == null || packagesConfig.Length == 0)
            {
                Logger.LogDebug("Packages.config file not found.");
                return Task.FromResult(true);
            }

            try
            {
                XDocument document;
                using (var stream = File.Open(packagesConfig[0].FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    document = XDocument.Load(stream);
                }

                XNamespace nsSys = "http://schemas.microsoft.com/developer/msbuild/2003";
                var existingPackageReferences = projectFile.Root.Elements(nsSys + "ItemGroup").Elements(nsSys + "PackageReference").Select(x => new PackageReference
                {
                    Id = x.Attribute("Include").Value,
                    Version = x.Attribute("Version")?.Value ?? x.Element(nsSys + "Version").Value,
                    IsDevelopmentDependency = x.Element(nsSys + "PrivateAssets") != null
                });

                var testReferences = Array.Empty<PackageReference>();
                if (definition.Type == ApplicationType.TestProject && existingPackageReferences.All(x => x.Id != "Microsoft.NET.Test.Sdk"))
                {
                    testReferences = new[]
                    {
                        new PackageReference { Id = "Microsoft.NET.Test.Sdk", Version = "15.0.0" },
                        new PackageReference { Id = "MSTest.TestAdapter", Version = "1.1.11" },
                        new PackageReference { Id = "MSTest.TestFramework", Version = "1.1.11" }
                    };

                    var versions = definition.TargetFrameworks?
                        .Select(f => int.TryParse(f.Replace("net", string.Empty), out int result) ? result : default(int?))
                        .Where(x => x.HasValue)
                        .Select(v => v < 100 ? v * 10 : v);

                    if (versions != null)
                    {
                        if (versions.Any(v => v < 450))
                        {
                            Logger.LogWarning($"Warning - target framework net40 is not compatible with the MSTest NuGet packages. Please consider updating the target framework of your test project(s)");
                        }
                    }
                }

                definition.PackageReferences = document.Element("packages").Elements("package").Select(x => new PackageReference
                {
                    Id = x.Attribute("id").Value,
                    Version = x.Attribute("version").Value,
                    IsDevelopmentDependency = x.Attribute("developmentDependency")?.Value == "true"
                })
                .Concat(testReferences)
                .Concat(existingPackageReferences)
                .ToArray();

                foreach (var reference in definition.PackageReferences)
                {
                    Logger.LogDebug($"Found nuget reference to {reference.Id}, version {reference.Version}.");
                }
            }
            catch(XmlException e)
            {
                Logger.LogError($"Got xml exception reading packages.config: " + e.Message);
            }

            return Task.FromResult(true);
        }
    }
}

using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Project2015To2017.Definition;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Project2015To2017
{
    internal sealed class ProjectReferenceTransformation : ITransformation
    {
        private readonly TransformationSettings transformationSettings;

        private ILogger Logger { get; set; }
        public ProjectReferenceTransformation(ILoggerFactory loggerFactory, TransformationSettings transformationSettings)
        {
            this.Logger = loggerFactory.CreateLogger<ProjectReferenceTransformation>();
            this.transformationSettings = transformationSettings;
        }
        public Task<bool> TransformAsync(bool prevTransformationResult, XDocument projectFile, DirectoryInfo projectFolder, Project definition)
        {
            XNamespace nsSys = "http://schemas.microsoft.com/developer/msbuild/2003";

            // TODO Project references are now transitive so we might be able to flatten later..

            definition.ProjectReferences = projectFile
				.Element(nsSys + "Project")
				.Elements(nsSys + "ItemGroup")
				.Elements(nsSys + "ProjectReference")
				.Select(x => new ProjectReference
				{
					Include = x.Attribute("Include").Value,
					Aliases = x.Element(nsSys + "Aliases")?.Value
				}).ToArray();
            return Task.FromResult(true);
        }
    }
}

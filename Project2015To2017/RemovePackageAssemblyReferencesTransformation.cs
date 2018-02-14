using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Project2015To2017.Definition;
using Microsoft.Extensions.Logging;

namespace Project2015To2017
{
    internal sealed class RemovePackageAssemblyReferencesTransformation : ITransformation
	{
        private readonly TransformationSettings transformationSettings;

        private ILogger Logger { get; set; }
        public RemovePackageAssemblyReferencesTransformation(ILoggerFactory loggerFactory, TransformationSettings transformationSettings)
        {
            this.Logger = loggerFactory.CreateLogger<RemovePackageAssemblyReferencesTransformation>();
            this.transformationSettings = transformationSettings;
        }
        public Task<bool> TransformAsync(bool prevTransformationResult, XDocument projectFile, DirectoryInfo projectFolder, Project definition)
        {
			if (definition.PackageReferences == null || definition.PackageReferences.Count == 0)
			{
				return Task.FromResult(true);
			}

			var packageReferenceIds = definition.PackageReferences.Select(x => x.Id).ToArray();
			definition.AssemblyReferences.RemoveAll(x => 
				x.HintPath != null && 
				packageReferenceIds.Any(p => x.HintPath.IndexOf(@"packages\" + p, StringComparison.OrdinalIgnoreCase) > 0));

			return Task.FromResult(true);
		}
	}
}

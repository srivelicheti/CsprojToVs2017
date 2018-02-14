using System.Collections.Generic;
using System.Xml.Linq;

namespace Project2015To2017.Definition
{

    internal sealed class Project
    {
        public string ProjFilePath { get; set; }
        public List<AssemblyReference> AssemblyReferences { get; internal set; }
        public IReadOnlyList<ProjectReference> ProjectReferences { get; internal set; }
        public IReadOnlyList<PackageReference> PackageReferences { get; internal set; }
        public IReadOnlyList<XElement> ItemsToInclude { get; internal set; }
        public PackageConfiguration PackageConfiguration { get; internal set; }
        public AssemblyAttributes AssemblyAttributes { get; internal set; }
        public XDocument[] ProjectImports { get; internal set; }
        public List<XElement> ImportedXElements { get; set; }
        public IReadOnlyList<XElement> ConditionalPropertyGroups { get; internal set; }

        public IReadOnlyList<XElement> UnConditionalPropertyGroups { get; internal set; }

        public IReadOnlyList<string> TargetFrameworks { get; internal set; }
        public bool OutPutTargetFrameworkType { get; set; } = true;
        public ApplicationType Type { get; internal set; }
        public bool OutPutApplicationTypeToProj { get; set; } = true;
        public bool Optimize { get; internal set; }
        public bool TreatWarningsAsErrors { get; internal set; }
        public string RootNamespace { get; internal set; }
        public string AssemblyName { get; internal set; }
        public bool AllowUnsafeBlocks { get; internal set; }
        public bool SignAssembly { get; internal set; }
        public string AssemblyOriginatorKeyFile { get; internal set; }

        //Customized 

        public string FileAlignment { get; set; }
        public string SchemaVersion { get; set; }
        public string AppDesignerFolder { get; set; }

    }
}

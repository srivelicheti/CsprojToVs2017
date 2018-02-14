using Microsoft.Extensions.Logging;
using Project2015To2017.Definition;
using Project2015To2017.Writing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Project2015To2017
{
    internal sealed class ProjectPropertiesTransformation : ITransformation
    {
        private readonly TransformationSettings _transformationSettings;

        private ILogger Logger { get; set; }
        public ProjectPropertiesTransformation(ILoggerFactory loggerFactory, TransformationSettings transformationSettings)
        {
            this.Logger = loggerFactory.CreateLogger<ProjectPropertiesTransformation>();
            this._transformationSettings = transformationSettings;
        }
        public Task<bool> TransformAsync(bool prevTransResult, XDocument projectFile, DirectoryInfo projectFolder, Project definition)
        {
            XNamespace nsSys = "http://schemas.microsoft.com/developer/msbuild/2003";
            var propertyGroups = projectFile.Element(nsSys + "Project").Elements(nsSys + "PropertyGroup").ToList();
            var importedPropertyGroups = new List<XElement>();
            foreach (var importedProject in definition.ProjectImports)
            {
                var groups = importedProject.IsVs2015ProjFileFormat() ? importedProject.Element(nsSys + "Project").Elements(nsSys + "PropertyGroup")
                    : importedProject.Element("Project").Elements("PropertyGroup");
                importedPropertyGroups.AddRange(groups);
            }

            // propertyGroups.AddRange(importedPropertyGroups);
			var unconditionalPropertyGroups = propertyGroups.Where(x => x.Attribute("Condition") == null).ToList();
            var importedUnconditionalPropertyGroups = importedPropertyGroups.Where(x => x.Attribute("Condition") == null);
            
            
			if(unconditionalPropertyGroups.Count() > 0 || importedUnconditionalPropertyGroups.Count() > 0)
            {
				var targetFramework = unconditionalPropertyGroups.Elements(nsSys + "TargetFrameworkVersion").FirstOrDefault()?.Value;
                if (string.IsNullOrEmpty(targetFramework))
                {
                    targetFramework = importedUnconditionalPropertyGroups.Elements(nsSys + "TargetFrameworkVersion").FirstOrDefault()?.Value;
                    definition.OutPutTargetFrameworkType = false;
                }
                

				definition.Optimize = "true".Equals(unconditionalPropertyGroups.Elements(nsSys + "Optimize").FirstOrDefault()?.Value, StringComparison.OrdinalIgnoreCase);
				definition.TreatWarningsAsErrors = "true".Equals(unconditionalPropertyGroups.Elements(nsSys + "TreatWarningsAsErrors").FirstOrDefault()?.Value, StringComparison.OrdinalIgnoreCase);
				definition.AllowUnsafeBlocks = "true".Equals(unconditionalPropertyGroups.Elements(nsSys + "AllowUnsafeBlocks").FirstOrDefault()?.Value, StringComparison.OrdinalIgnoreCase);

				definition.RootNamespace = unconditionalPropertyGroups.Elements(nsSys + "RootNamespace").FirstOrDefault()?.Value;
				definition.AssemblyName = unconditionalPropertyGroups.Elements(nsSys + "AssemblyName").FirstOrDefault()?.Value;

                definition.SignAssembly = "true".Equals(unconditionalPropertyGroups.Elements(nsSys + "SignAssembly").FirstOrDefault()?.Value, StringComparison.OrdinalIgnoreCase);
                definition.AssemblyOriginatorKeyFile = unconditionalPropertyGroups.Elements(nsSys + "AssemblyOriginatorKeyFile").FirstOrDefault()?.Value;
                //definition.FileAlignment = unconditionalPropertyGroups.Elements(nsSys + "FileAlignment").FirstOrDefault()?.Value;
                //definition.SchemaVersion = unconditionalPropertyGroups.Elements(nsSys + "SchemaVersion").FirstOrDefault()?.Value;
                //definition.AppDesignerFolder = unconditionalPropertyGroups.Elements(nsSys + "AppDesignerFolder").FirstOrDefault()?.Value;

                // Ref.: https://www.codeproject.com/Reference/720512/List-of-Visual-Studio-Project-Type-GUIDs
                if (unconditionalPropertyGroups.Elements(nsSys + "TestProjectType").Any() || 
                    unconditionalPropertyGroups.Elements(nsSys + "ProjectTypeGuids").
                        Any(e => e.Value.IndexOf("3AC096D0-A1C2-E12C-1390-A8335801FDAB", StringComparison.OrdinalIgnoreCase) > -1))
                {
                    definition.Type = ApplicationType.TestProject;
                }
                else if (importedUnconditionalPropertyGroups.Elements(nsSys + "TestProjectType").Any() ||
                    importedUnconditionalPropertyGroups.Elements(nsSys + "ProjectTypeGuids").
                        Any(e => e.Value.IndexOf("3AC096D0-A1C2-E12C-1390-A8335801FDAB", StringComparison.OrdinalIgnoreCase) > -1))
                {
                    definition.Type = ApplicationType.TestProject;
                }
                else
                {
                    definition.Type = ToApplicationType(unconditionalPropertyGroups.Elements(nsSys + "OutputType").FirstOrDefault()?.Value);
                    if(definition.Type == ApplicationType.Unknown)
                    {
                        definition.Type = ToApplicationType(importedUnconditionalPropertyGroups.Elements(nsSys + "OutputType").FirstOrDefault()?.Value);
                        definition.OutPutApplicationTypeToProj = false;
                    }
                }

                if (targetFramework != null)
                {
                    definition.TargetFrameworks = new[] { ToTargetFramework(targetFramework) };
                }
                else {
                    definition.TargetFrameworks = GetNewFormatTargetFrameworkVersion(unconditionalPropertyGroups);
                    if(definition.TargetFrameworks == null || definition.TargetFrameworks.Count == 0)
                    {
                        definition.TargetFrameworks = GetNewFormatTargetFrameworkVersion(importedUnconditionalPropertyGroups);
                    }
                    if(definition.TargetFrameworks == null) //target framework not determined
                        Logger.LogWarning($"Target Framework not determined for {definition.ProjFilePath}");
                }
			}
            UpdateTargetFrameworkVersionInConditionalPropertyGorups(propertyGroups);
            definition.ConditionalPropertyGroups = propertyGroups.Where(x => x.Attribute("Condition") != null).ToArray();
            RemoveUsedUnConditionalPropertyGroupElements(nsSys, unconditionalPropertyGroups);
            definition.UnConditionalPropertyGroups = unconditionalPropertyGroups.Where(x => x.HasElements).ToList();

            if (definition.Type == ApplicationType.Unknown)
            {
                //projectFile.Root.
                return Task.FromResult<bool>(false);
                //throw new NotSupportedException("Unable to parse output type.");
            }


            return Task.FromResult<bool>(true);
        }

        private void UpdateTargetFrameworkVersionInConditionalPropertyGorups(List<XElement> propertyGroups) {
            var condPropGroups = propertyGroups.Where(x => x.Attribute("Condition") != null);
            foreach(var group in condPropGroups)
            {
                if(group.Element("TargetFrameworkVersion") != null)
                {
                    var el = group.Element("TargetFrameworkVersion");
                    el.Name = "TargetFramework";
                    el.Value = ToTargetFramework(el.Value);
                    
                }
            }
        }

        private string[] GetNewFormatTargetFrameworkVersion(IEnumerable<XElement> unconditionalPropertyGroups)
        {
           var fw = unconditionalPropertyGroups.Elements("TargetFramework").FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(fw)) return new string[] { fw };
            else {
                return unconditionalPropertyGroups.Elements("TargetFrameworks").FirstOrDefault()?.Value?.Split(';');
            }
        }

        private void RemoveUsedUnConditionalPropertyGroupElements(XNamespace nsSys, List<XElement> unconditionalPropertyGroups)
        {
            unconditionalPropertyGroups.Elements(nsSys + "TargetFrameworkVersion").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "Optimize").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "TreatWarningsAsErrors").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "AllowUnsafeBlocks").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "RootNamespace").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "AssemblyName").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "SignAssembly").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "AssemblyOriginatorKeyFile").FirstOrDefault()?.Remove();
            //unconditionalPropertyGroups.Elements(nsSys + "FileAlignment").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "SchemaVersion").FirstOrDefault()?.Remove();
            //unconditionalPropertyGroups.Elements(nsSys + "AppDesignerFolder").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "TestProjectType").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "OutputType").FirstOrDefault()?.Remove();
            unconditionalPropertyGroups.Elements(nsSys + "ProjectGuid").FirstOrDefault()?.Remove();
            
            // Ref.: https://www.codeproject.com/Reference/720512/List-of-Visual-Studio-Project-Type-GUIDs

        }

        private string ToTargetFramework(string targetFramework)
        {
            if (targetFramework.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                return "net" + targetFramework.Substring(1).Replace(".", string.Empty);
            }
            else if (targetFramework.StartsWith("profile", StringComparison.OrdinalIgnoreCase))
                return "netstandard1.6";

            throw new NotSupportedException($"Target framework {targetFramework} is not supported.");
        }

		private ApplicationType ToApplicationType(string outputType)
		{
			if (string.IsNullOrWhiteSpace(outputType))
			{
				return ApplicationType.Unknown;
			}

			switch (outputType.ToLowerInvariant())
			{
				case "exe": return ApplicationType.ConsoleApplication;
				case "library": return ApplicationType.ClassLibrary;
				case "winexe": return ApplicationType.WindowsApplication;
				default: throw new NotSupportedException($"OutputType {outputType} is not supported.");
			}
		}
    }
}

﻿using Project2015To2017.Definition;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Project2015To2017.Writing
{
    internal static class Extensions {
        public static void AddIfNotEmpty(this XElement element, XElement added)
        {
            if(added != null && (added.HasAttributes || added.HasElements))
            {
                element.Add(added);
            }
        }

        public static void AddIfNotEmpty(this XElement element, IEnumerable<XElement> added)
        {
            if(added != null && added.Count() > 0)
            {
                foreach(var el in added)
                {
                    AddIfNotEmpty(element, el);
                }
            }
        }

        public static bool IsVs2015ProjFileFormat(this XDocument document)
        {
            XNamespace nsSys = "http://schemas.microsoft.com/developer/msbuild/2003";
            return document.Element(nsSys + "Project") != null;
        }
    }
    internal sealed class ProjectWriter
    {
        public void Write(Project project, FileInfo outputFile)
        {
            var projectNode = CreateXml(project, outputFile);

            using (var filestream = File.Open(outputFile.FullName, FileMode.Create))
            using (var streamWriter = new StreamWriter(filestream, Encoding.UTF8))
            {
                streamWriter.Write(projectNode.ToString());
            }
        }

        internal XElement CreateXml(Project project, FileInfo outputFile)
        {
            var projectNode = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"));

            projectNode.AddIfNotEmpty(GetMainPropertyGroup(project, outputFile));
            projectNode.AddIfNotEmpty(GetAdditionalPropertyGroup(project, outputFile));


            if (project.ImportedXElements != null)
                projectNode.AddIfNotEmpty(project.ImportedXElements.Select(RemoveAllNamespaces));
            if (project.UnConditionalPropertyGroups != null)
                projectNode.AddIfNotEmpty(project.UnConditionalPropertyGroups.Select(RemoveAllNamespaces));

            if (project.ConditionalPropertyGroups != null)
            {
                projectNode.AddIfNotEmpty(project.ConditionalPropertyGroups.Select(RemoveAllNamespaces));
            }

            if (project.ProjectReferences?.Count > 0)
            {
                var itemGroup = new XElement("ItemGroup");
                foreach (var projectReference in project.ProjectReferences)
                {
                    var projectReferenceElement = new XElement("ProjectReference",
                            new XAttribute("Include", projectReference.Include));

                    if (!string.IsNullOrWhiteSpace(projectReference.Aliases) && projectReference.Aliases != "global")
                    {
                        projectReferenceElement.Add(new XElement("Aliases", projectReference.Aliases));
                    }

                    itemGroup.Add(projectReferenceElement);
                }

                projectNode.Add(itemGroup);
            }

            if (project.PackageReferences?.Count > 0)
            {
                var nugetReferences = new XElement("ItemGroup");
                foreach (var packageReference in project.PackageReferences)
                {
                    var reference = new XElement("PackageReference", new XAttribute("Include", packageReference.Id), new XAttribute("Version", packageReference.Version));
                    if (packageReference.IsDevelopmentDependency)
                    {
                        reference.Add(new XElement("PrivateAssets", "all"));
                    }

                    nugetReferences.Add(reference);
                }

                projectNode.Add(nugetReferences);
            }

            if (project.AssemblyReferences?.Count > 0)
            {
                var assemblyReferences = new XElement("ItemGroup");
                foreach (var assemblyReference in project.AssemblyReferences.Where(x => !IsDefaultIncludedAssemblyReference(x.Include)))
                {
                    assemblyReferences.Add(MakeAssemblyReference(assemblyReference));
                }

                projectNode.Add(assemblyReferences);
            }

            // manual includes
            if (project.ItemsToInclude?.Count > 0)
            {
                var includeGroup = new XElement("ItemGroup");
                foreach (var include in project.ItemsToInclude.Select(RemoveAllNamespaces))
                {
                    var linked = include.Element("Link");
                    if ( linked != null) {
                        linked.Remove();
                        include.SetAttributeValue("Link", linked.Value);
                    }
                    includeGroup.Add(include);
                }

                projectNode.Add(includeGroup);
            }

            return projectNode;
        }

        private static XElement MakeAssemblyReference(AssemblyReference assemblyReference)
        {
            var output = new XElement("Reference", new XAttribute("Include", assemblyReference.Include));

            if (assemblyReference.HintPath != null)
            {
                output.Add(new XElement("HintPath", assemblyReference.HintPath));
            }
            if (assemblyReference.Private != null)
            {
                output.Add(new XElement("Private", assemblyReference.Private));
            }
            if (assemblyReference.SpecificVersion != null)
            {
                output.Add(new XElement("SpecificVersion", assemblyReference.SpecificVersion));
            }
            if (assemblyReference.EmbedInteropTypes != null)
            {
                output.Add(new XElement("EmbedInteropTypes", assemblyReference.EmbedInteropTypes));
            }

            return output;
        }

        private static XElement RemoveAllNamespaces(XElement e)
        {
            return new XElement(e.Name.LocalName,
              (from n in e.Nodes()
               select ((n is XElement) ? RemoveAllNamespaces((XElement)n) : n)),
                  (e.HasAttributes) ?
                    (from a in e.Attributes()
                     where (!a.IsNamespaceDeclaration)
                     select new XAttribute(a.Name.LocalName, a.Value)) : null);
        }

        private bool IsDefaultIncludedAssemblyReference(string assemblyReference)
        {
            return new[]
            {
                "System",
                "System.Core",
                "System.Data",
                "System.Drawing",
                "System.IO.Compression.FileSystem",
                "System.Numerics",
                "System.Runtime.Serialization",
                "System.Xml",
                "System.Xml.Linq"
            }.Contains(assemblyReference);
        }

        private XElement GetMainPropertyGroup(Project project, FileInfo outputFile)
        {
            var mainPropertyGroup = new XElement("PropertyGroup");
            //if(project.OutPutTargetFrameworkType)
            AddTargetFrameworks(mainPropertyGroup, project.TargetFrameworks);

            AddIfNotNull(mainPropertyGroup, "Optimize", project.Optimize ? "true" : null);
            AddIfNotNull(mainPropertyGroup, "TreatWarningsAsErrors", project.TreatWarningsAsErrors ? "true" : null);
            AddIfNotNull(mainPropertyGroup, "RootNamespace", project.RootNamespace != Path.GetFileNameWithoutExtension(outputFile.Name) ? project.RootNamespace : null);
            AddIfNotNull(mainPropertyGroup, "AssemblyName", project.AssemblyName != Path.GetFileNameWithoutExtension(outputFile.Name) ? project.AssemblyName : null);
            AddIfNotNull(mainPropertyGroup, "AllowUnsafeBlocks", project.AllowUnsafeBlocks ? "true" : null);
            AddIfNotNull(mainPropertyGroup, "SignAssembly", project.SignAssembly ? "true" : null);
            AddIfNotNull(mainPropertyGroup, "AssemblyOriginatorKeyFile", project.AssemblyOriginatorKeyFile);
            //if (project.ItemsToInclude.Count > 0)
            //{
            //    AddIfNotNull(mainPropertyGroup, "EnableDefaultCompileItems", "false");
            //}
            if (project.OutPutApplicationTypeToProj)
            {
                switch (project.Type)
                {
                    case ApplicationType.ConsoleApplication:
                        mainPropertyGroup.Add(new XElement("OutputType", "Exe"));
                        break;
                    case ApplicationType.WindowsApplication:
                        mainPropertyGroup.Add(new XElement("OutputType", "WinExe"));
                        break;
                }
            }
            AddAssemblyAttributeNodes(mainPropertyGroup, project.AssemblyAttributes);
            AddPackageNodes(mainPropertyGroup, project.PackageConfiguration, project.AssemblyAttributes);

            return mainPropertyGroup;
        }

        private XElement GetAdditionalPropertyGroup(Project project, FileInfo outputFile)
        {
            var mainPropertyGroup = new XElement("PropertyGroup");
                        
            AddIfNotNull(mainPropertyGroup, "FileAlignment", project.FileAlignment);
            AddIfNotNull(mainPropertyGroup, "SchemaVersion", project.SchemaVersion);
            AddIfNotNull(mainPropertyGroup, "AppDesignerFolder", project.AppDesignerFolder);

            return mainPropertyGroup;
        }

        private void AddPackageNodes(XElement mainPropertyGroup, PackageConfiguration packageConfiguration, AssemblyAttributes attributes)
        {
            if (packageConfiguration == null)
            {
                return;
            }

            AddIfNotNull(mainPropertyGroup, "Company", attributes?.Company);
            AddIfNotNull(mainPropertyGroup, "Authors", packageConfiguration.Authors);
            AddIfNotNull(mainPropertyGroup, "Copyright", packageConfiguration.Copyright);
            AddIfNotNull(mainPropertyGroup, "Description", packageConfiguration.Description);
            AddIfNotNull(mainPropertyGroup, "PackageIconUrl", packageConfiguration.IconUrl);
            AddIfNotNull(mainPropertyGroup, "PackageId", packageConfiguration.Id);
            AddIfNotNull(mainPropertyGroup, "PackageLicenseUrl", packageConfiguration.LicenseUrl);
            AddIfNotNull(mainPropertyGroup, "PackageProjectUrl", packageConfiguration.ProjectUrl);
            AddIfNotNull(mainPropertyGroup, "PackageReleaseNotes", packageConfiguration.ReleaseNotes);
            AddIfNotNull(mainPropertyGroup, "PackageTags", packageConfiguration.Tags);
            AddIfNotNull(mainPropertyGroup, "PackageVersion", packageConfiguration.Version);

            if (packageConfiguration.RequiresLicenseAcceptance)
            {
                mainPropertyGroup.Add(new XElement("PackageRequireLicenseAcceptance", "true"));
            }
        }

        private void AddAssemblyAttributeNodes(XElement mainPropertyGroup, AssemblyAttributes assemblyAttributes)
        {
            if (assemblyAttributes == null)
            {
                return;
            }

            var attributes = new[]
            {
                new KeyValuePair<string, string>("GenerateAssemblyTitleAttribute", assemblyAttributes.Title),
                new KeyValuePair<string, string>("GenerateAssemblyCompanyAttribute", assemblyAttributes.Company),
                new KeyValuePair<string, string>("GenerateAssemblyDescriptionAttribute", assemblyAttributes.Description),
                new KeyValuePair<string, string>("GenerateAssemblyProductAttribute", assemblyAttributes.Product),
                new KeyValuePair<string, string>("GenerateAssemblyCopyrightAttribute", assemblyAttributes.Copyright),
                new KeyValuePair<string, string>("GenerateAssemblyInformationalVersionAttribute", assemblyAttributes.InformationalVersion),
                new KeyValuePair<string, string>("GenerateAssemblyVersionAttribute", assemblyAttributes.Version),
                new KeyValuePair<string, string>("GenerateAssemblyFileVersionAttribute", assemblyAttributes.FileVersion),
                new KeyValuePair<string, string>("GenerateAssemblyConfigurationAttribute", assemblyAttributes.Configuration)
            };

            var childNodes = attributes
                .Where(x => x.Value != null)
                .Select(x => new XElement(x.Key, "false"))
                .ToArray();

            if (childNodes.Length == 0)
            {
                mainPropertyGroup.Add(new XElement("GenerateAssemblyInfo", "false"));
            }
            else
            {
                mainPropertyGroup.Add(childNodes);
            }
        }

        private void AddIfNotNull(XElement node, string elementName, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                node.Add(new XElement(elementName, value));
            }
        }

        private void AddTargetFrameworks(XElement mainPropertyGroup, IReadOnlyList<string> targetFrameworks)
        {
            if (targetFrameworks == null)
            {
                return;
            }
            else if (targetFrameworks.Count > 1)
            {
                AddIfNotNull(mainPropertyGroup, "TargetFrameworks", string.Join(";", targetFrameworks));
            }
            else
            {
                AddIfNotNull(mainPropertyGroup, "TargetFramework", targetFrameworks[0]);
            }
        }
    }
}

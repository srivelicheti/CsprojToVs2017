using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Project2015To2017.Definition;
using System.Xml.Linq;

namespace Project2015To2017
{
    //class ProjectImportTransformation : ITransformation
    //{
    //    public Task<bool> TransformAsync(XDocument projectFile, DirectoryInfo projectFolder, Project definition)
    //    {
    //        var imports = projectFile.Descendants().Where(x => x.Name.LocalName == "Import").ToList();
    //        foreach (var im in imports)
    //        {
    //            var path = im.Attribute("Project").Value;
    //            try
    //            {
    //                var doc = XDocument.Load(Path.Combine(projectFolder.ToString(), path));
    //                im.ReplaceWith(doc.Elements().First().Elements());
    //            }catch(Exception ex) { }
    //        }
    //        projectFile.Nodes().Where(x =>
    //        {
    //            return x.BaseUri == "Test";
    //        });

    //        return Task.FromResult(0);
    //    }
    //}
}

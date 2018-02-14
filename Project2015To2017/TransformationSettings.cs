using CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace Project2015To2017
{
    public class TransformationSettings
    {
        [Option('r', "removefiles", Required = false, HelpText = "Remove files that are there in the project folder but not included in proj file")]
        public bool RemoveFilesNotIncludedInProj { get; set; }

        [Option('p', "path", Required = true, HelpText = @"Path to file\folder to look for proj files")]
        public string Path { get; set; }

        [Option('e', "exclude", Required = false, HelpText = @"Path to file\folder to look for proj files")]
        public string ExcludeProjectsMatching { get; set; }

        [Option('c', "copy", Required = false, HelpText = @"Path to file\folder to look for proj files")]
        public bool CopyRelativelyIncludedParentDirectoryFiles { get; set; }

    }
}

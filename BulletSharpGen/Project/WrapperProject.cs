﻿using System;
using System.Collections.Generic;
using System.IO;

namespace BulletSharpGen
{
    class WrapperProject
    {
        public string ProjectPath { get; set; }

        public string NamespaceName { get; set; }
        public Dictionary<string, ClassDefinition> ClassDefinitions = new Dictionary<string, ClassDefinition>();
        public Dictionary<string, HeaderDefinition> HeaderDefinitions = new Dictionary<string, HeaderDefinition>();

        public ISymbolMapping ClassNameMapping { get; set; }
        public ISymbolMapping HeaderNameMapping { get; set; }
        public ISymbolMapping MethodNameMapping { get; set; }
        public ISymbolMapping ParameterNameMapping { get; set; }

        public List<string> SourceRootFolders { get; set; }

        public WrapperProject()
        {
            SourceRootFolders = new List<string>();
        }

        public static string MakeRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
            if (string.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            var fromUri = new Uri(fromPath);
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.ToUpperInvariant() == "FILE")
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        public bool VerifyFiles()
        {
            foreach (string sourceFolder in SourceRootFolders)
            {
                if (!Directory.Exists(sourceFolder))
                {
                    Console.WriteLine("Source folder \"" + sourceFolder + "\" not found");
                    return false;
                }
            }
            return true;
        }

        public static WrapperProject FromFile(string filename)
        {
            var project = new WrapperProject();
            project.ProjectPath = Path.GetFullPath(filename);

            Project.WrapperProjectXmlReader.Read(project);

            return project;
        }

        public void Save()
        {
            Project.WrapperProjectXmlWriter.Write(this);
        }
    }
}

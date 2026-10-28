using System;
using System.IO;

namespace GConfig.Parser
{
    public class CatalogConfigAttribute : Attribute
    {
        public string Path { get; private set; }
        public ParsePhase LoadPhases { get; private set; }
        public CatalogConfigAttribute(string path, ParsePhase loadPhases = ParsePhase.None)
        {
            if (!string.IsNullOrEmpty(path) && string.IsNullOrEmpty(System.IO.Path.GetExtension(path)) && !path.EndsWith("/")) path += "/";
            Path = path;
            LoadPhases = loadPhases;
        }
    }
}

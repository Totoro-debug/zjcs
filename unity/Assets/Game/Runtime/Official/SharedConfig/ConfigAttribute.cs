using System;
using System.IO;

namespace GConfig
{
    // Recovered from APK Config.dll metadata and native 0x9918ea8.
    public enum ParsePhase { None = 1, Launch = 2, Game = 4 }

    public class ConfigAttribute : Attribute
    {
        public string Path { get; private set; }
        public Type ParserBaseType { get; private set; }
        public ParsePhase LoadPhases { get; private set; }

        public ConfigAttribute(string path, Type parserBaseType, ParsePhase loadPhases = ParsePhase.None)
        {
            if (!string.IsNullOrEmpty(path) && string.IsNullOrEmpty(System.IO.Path.GetExtension(path)) && !path.EndsWith("/"))
                path += "/";
            Path = path;
            ParserBaseType = parserBaseType;
            LoadPhases = loadPhases;
        }
    }
}

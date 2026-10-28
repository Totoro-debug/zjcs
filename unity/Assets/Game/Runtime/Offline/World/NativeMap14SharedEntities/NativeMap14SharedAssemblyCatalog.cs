using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    public sealed class NativeMap14SharedAssemblyCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class AssemblyPayload
        {
            public string Name;
            public TextAsset Data;
            public string Sha256;
            public string ModuleId;
        }

        public AssemblyPayload[] Assemblies;
        public TextAsset Prefabs;
        public string PrefabsSha256;
    }
}

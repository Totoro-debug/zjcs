using System;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Offline.PartnersNpc
{
    public sealed class OfficialPartnerNpcRuntimeCatalog : ScriptableObject
    {
        [Serializable] public sealed class Table
        {
            public string Location, Sha256;
            public TextAsset Data;
        }
        [SerializeField] private string manifest;
        [SerializeField] private NativeMap14SharedAssemblyCatalog dependencies;
        [SerializeField] private NativeMap14SharedAssemblyCatalog.AssemblyPayload[] assemblies;
        [SerializeField] private Table[] tables;
        [SerializeField] private string propertyBinarySha256;
        public NativeMap14SharedAssemblyCatalog Dependencies => dependencies;
        public NativeMap14SharedAssemblyCatalog.AssemblyPayload[] Assemblies => assemblies;
        public Table[] Tables => tables;
        public string PropertyBinarySha256 => propertyBinarySha256;

        public static OfficialPartnerNpcRuntimeCatalog Load()
        {
            var result = Resources.Load<OfficialPartnerNpcRuntimeCatalog>("Partners/OfficialPartnerNpcRuntimeCatalog");
            if (!result || result.manifest != "884_156692" || !result.dependencies ||
                result.assemblies == null || result.assemblies.Length != 3 || result.tables == null || result.tables.Length != 14 ||
                string.IsNullOrEmpty(result.propertyBinarySha256))
                throw new InvalidOperationException("Selected original NPC runtime catalog is incomplete: loaded=" + (bool)result +
                    (result ? ", manifest=" + result.manifest + ", dependencies=" + (bool)result.dependencies +
                    ", modules=" + result.assemblies?.Length + ", tables=" + result.tables?.Length + ", binary=" + result.propertyBinarySha256 : ""));
            return result;
        }
    }
}

using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Original atlas object metadata and ordered native material references.
    // Geometry regions remain in the lossless skeleton carrier.
    public sealed class ProfessionNativeTextureAtlas : ScriptableObject
    {
        [SerializeField] private string sourcePath, sourceGuid;
        [SerializeField] private TextAsset originalMetadata;
        [SerializeField] private Material[] materials;
        public string SourcePath => sourcePath;
        public string SourceGuid => sourceGuid;
        public TextAsset OriginalMetadata => originalMetadata;
        public int MaterialCount => materials.Length;
        public Material MaterialAt(int index)
        {
            if ((uint)index >= materials.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return materials[index];
        }
    }
}

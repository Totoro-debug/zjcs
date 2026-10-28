using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Native carrier for verified source metadata and the losslessly converted
    // source data. It neither embeds nor invokes an animation SDK.
    public sealed class ProfessionNativeSkeletonAsset : ScriptableObject
    {
        [Serializable] private sealed class Settings
        {
            public float scale, defaultMix;
            public string[] fromAnimation, toAnimation, zSlots;
            public float[] duration;
        }
        [SerializeField] private string sourcePath, sourceGuid, sex;
        [SerializeField] private TextAsset payload, originalMetadata;
        private ProfessionNativeSkeletonData data;
        private Settings settings;
        public string SourcePath => sourcePath;
        public string SourceGuid => sourceGuid;
        public string Sex => sex;
        public string OriginalMetadata => originalMetadata ? originalMetadata.text : throw new InvalidOperationException("Original skeleton metadata is unbound.");
        public float DefaultMix => Configuration.defaultMix;
        public IReadOnlyList<string> ZSlots => Array.AsReadOnly(Configuration.zSlots);
        public IReadOnlyList<string> FromAnimation => Array.AsReadOnly(Configuration.fromAnimation);
        public IReadOnlyList<string> ToAnimation => Array.AsReadOnly(Configuration.toAnimation);
        public IReadOnlyList<float> MixDurations => Array.AsReadOnly(Configuration.duration);
        private Settings Configuration
        {
            get
            {
                if (settings != null) return settings;
                var result = JsonUtility.FromJson<Settings>(OriginalMetadata);
                if (result == null || !(result.scale > 0) || result.defaultMix < 0 ||
                    result.fromAnimation == null || result.toAnimation == null || result.duration == null || result.zSlots == null ||
                    result.fromAnimation.Length != result.toAnimation.Length || result.fromAnimation.Length != result.duration.Length)
                    throw new InvalidOperationException("Original skeleton settings are invalid.");
                return settings = result;
            }
        }
        public ProfessionNativeSkeletonData Data
        {
            get
            {
                if (data != null) return data;
                if (!payload) throw new InvalidOperationException("Native original skeleton payload is unbound.");
                var result = ProfessionNativeSkeletonData.Load(payload.bytes);
                if (result.Sex != sex || result.DataScale != Configuration.scale)
                    throw new InvalidOperationException("Original skeleton carrier sex or scale differs from its payload.");
                return data = result;
            }
        }
    }
}

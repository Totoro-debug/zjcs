using System;
using System.IO;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // The current scene input stays separate from the historical profession data.
    public sealed class ProfessionNativeSceneCharacterAsset : ScriptableObject
    {
        public const string SourceManifest = "884_156692";
        [SerializeField] private string sex, skeletonSha256, atlasSha256;
        [SerializeField] private TextAsset payload, hierarchy;
        private ProfessionNativeSkeletonData data;

        public TextAsset Hierarchy => hierarchy ? hierarchy : throw new InvalidDataException("Original scene hierarchy is unbound.");
        public ProfessionNativeSkeletonData Data
        {
            get
            {
                if (data != null) return data;
                if (!payload || sex != "male" && sex != "female")
                    throw new InvalidDataException("Original scene character input is unbound.");
                var loaded = ProfessionNativeSkeletonData.Load(payload.bytes, SourceManifest);
                if (loaded.Sex != sex || loaded.SkeletonSha256 != skeletonSha256 || loaded.AtlasSha256 != atlasSha256)
                    throw new InvalidDataException("Original scene character source hashes differ.");
                return data = loaded;
            }
        }

        public static ProfessionNativeSceneCharacterAsset LoadResource(string sex)
        {
            if (sex != "male" && sex != "female") throw new ArgumentException("Unknown original scene character sex.", nameof(sex));
            var asset = Resources.Load<ProfessionNativeSceneCharacterAsset>("Combat/SceneCharacters/" + sex);
            if (!asset || asset.sex != sex) throw new InvalidDataException("Original scene character resource is unavailable: " + sex);
            return asset;
        }
    }
}

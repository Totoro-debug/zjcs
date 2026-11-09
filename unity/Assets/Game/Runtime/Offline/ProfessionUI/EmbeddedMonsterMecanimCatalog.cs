using System;
using System.IO;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class EmbeddedMonsterMecanimCatalog : ScriptableObject
    {
        [SerializeField] private GameObject sourcePrefab;
        [SerializeField] private string skeletonPath;
        [SerializeField] private int modelId;
        [SerializeField] private BattleNativeMonsterAssets nativeAssets;
        [SerializeField] private ProfessionNativeWeaponTextureResources textureResources;
        public GameObject SourcePrefab => sourcePrefab;
        public string SkeletonPath => skeletonPath;
        public int ModelId => modelId;
        public BattleNativeMonsterAssets NativeAssets => nativeAssets;
        public ProfessionNativeWeaponTextureResources TextureResources => textureResources;
        public static EmbeddedMonsterMecanimCatalog Load()
        {
            var result = Resources.Load<EmbeddedMonsterMecanimCatalog>("Combat/EmbeddedMonsterMecanim/catalog");
            if (!result || !result.sourcePrefab || !result.nativeAssets || !result.textureResources
                || result.modelId != 3205 || string.IsNullOrEmpty(result.skeletonPath))
                throw new InvalidDataException("Bind the complete verified embedded Mecanim catalog.");
            return result;
        }
    }
}

using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Official.World.NativeEnvironment;

namespace Zjcs.Presentation.World.NativeMap12
{
    public sealed class OfficialMap12NativeWorldAssets : ScriptableObject
    {
        public NativeMap13AssetCatalog Terrain, Scene, Environment, Labels;
        public UniversalRenderPipelineAsset Pipeline;
        public MapEnvData EnvironmentData;
        public TMP_FontAsset Font;

        public static OfficialMap12NativeWorldAssets Load()
        {
            var asset = Resources.Load<OfficialMap12NativeWorldAssets>("Map12/Native/world-assets");
            if (!asset || !asset.Terrain || !asset.Scene || !asset.Environment || !asset.Labels || !asset.Pipeline
                || !asset.EnvironmentData || !asset.Font || asset.Terrain.Bindings.Length != 187
                || asset.Environment.Bindings.Length != 187 || asset.Labels.Bindings.Length != 80)
                throw new InvalidDataException("Map12 complete native world resources are unbound.");
            return asset;
        }
    }

    public interface IMap12NativeWorldState
    {
        Vector3 PlayerWorldPosition { get; }
        int PlayerLevel { get; }
        int PlayerSubRank { get; }
        long PlayerCombatRating { get; }
        bool IsPublicRegion(int region);
        int GetRegionProgress(int region);
        int GetRegionGoddessId(int region);
    }
}

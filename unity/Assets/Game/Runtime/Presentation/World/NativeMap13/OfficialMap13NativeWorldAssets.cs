using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Official.World.NativeEnvironment;

namespace Zjcs.Presentation.World.NativeMap13
{
    internal sealed class OfficialMap13NativeWorldAssets
    {
        public NativeMap13AssetCatalog Terrain, Scene, Environment, Labels;
        public UniversalRenderPipelineAsset Pipeline;
        public MapEnvData EnvironmentData;
        public TMP_FontAsset Font;

        internal static OfficialMap13NativeWorldAssets Load()
        {
            var result = new OfficialMap13NativeWorldAssets {
                Terrain = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-terrain-catalog"),
                Scene = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-scene-catalog"),
                Environment = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-environment-scene-catalog"),
                Labels = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-region-labels-catalog"),
                EnvironmentData = Resources.Load<MapEnvData>("Map13/Native/map-env-data")
            };
            var pipeline = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-render-pipeline-catalog");
            var text = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-text-catalog");
            if (!result.Terrain || !result.Scene || !result.Environment || !result.Labels || !pipeline || !text
                || !result.EnvironmentData || result.Terrain.Bindings.Length != 201
                || result.Scene.Bindings.Length != 202 || result.Environment.Bindings.Length != 183
                || result.Labels.Bindings.Length != 88)
                throw new InvalidDataException("Map13 original native world resources are incomplete.");
            result.Pipeline = pipeline.Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");
            result.Font = text.Load<TMP_FontAsset>("Assets/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset");
            return result;
        }
    }

    public interface IMap13NativeWorldState
    {
        Vector3 PlayerWorldPosition { get; }
        int PlayerLevel { get; }
        int PlayerSubRank { get; }
        long PlayerCombatRating { get; }
        bool IsPublicRegion(int region);
        int GetRegionProgress(int region);
        int GetRegionGoddessId(int region);
        void UnlockAllRegionPositions(int region);
    }
}

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        public static void RunPlayerResourceRoutes()
        {
            RunPlayerActors();
            SessionState.SetBool(Key + ".player-resource-routes", true);
        }

        private sealed class RejectTextureRoutes : IProfessionAppearanceTextureBindings
        {
            internal int Calls;
            public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : UnityEngine.Object
            { Calls++; throw new InvalidOperationException("Unregistered host texture request: " + guid); }
            public Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex)
            { Calls++; throw new InvalidOperationException("Unregistered host atlas request."); }
        }

        private sealed class CountTextureRoutes : IProfessionAppearanceTextureBindings
        {
            private readonly IProfessionAppearanceTextureBindings target;
            internal int Textures, Atlases;
            internal CountTextureRoutes(IProfessionAppearanceTextureBindings target) { this.target = target; }
            public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : UnityEngine.Object
            { Textures++; return target.LoadAssetSyncByGuid<T>(guid); }
            public Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex)
            { Atlases++; return target.GetAtlasMaterial(atlas, materialIndex); }
        }

        private static void CheckPlayerTextureRoutes(OfflineProfessionBattleSceneTextures scene,
            ProfessionNativeSceneAppearanceResources body, ProfessionNativeWeaponTextureResources weapons)
        {
            var host = (RejectTextureRoutes)ProfessionAppearanceTextureBindings.Current;
            string guid = body.TextureAddresses.First().Guid;
            var atlas = body.Models.First().Atlas;
            var a = new CountTextureRoutes(scene); var b = new CountTextureRoutes(scene);
            var first = ProfessionAppearanceTextureBindings.Register(a, new[] { guid }, new[] { atlas });
            IDisposable second = null;
            IProfessionUnsafeAssetHandle<Texture> heldA = null, heldB = null;
            try
            {
                heldA = ProfessionAppearanceTextureBindings.Required.LoadAssetSyncByGuid<Texture>(guid);
                Require(heldA.IsValid && heldA.Result && a.Textures == 1, "first registered packaged texture owner");
                second = ProfessionAppearanceTextureBindings.Register(b, new[] { guid }, new[] { atlas });
                heldB = ProfessionAppearanceTextureBindings.Required.LoadAssetSyncByGuid<Texture>(guid);
                var material = ProfessionAppearanceTextureBindings.Required.GetAtlasMaterial(atlas, 0);
                Require(material && heldB.Result == heldA.Result && b.Textures == 1 && b.Atlases == 1,
                    "overlapping owners share the verified packaged identity and route atlas operations");
                first.Dispose(); first.Dispose();
                Require(ProfessionAppearanceTextureBindings.Required.GetAtlasMaterial(atlas, 0) == material && b.Atlases == 2,
                    "out-of-order release preserves the remaining owner");
                heldA.Release(); heldA = null;
                Require(heldB.IsValid && heldB.Result, "releasing an earlier owner leaves the later texture handle valid");
                second.Dispose(); second = null;
                Require(ProfessionAppearanceTextureBindings.Required.GetAtlasMaterial(atlas, 0) == material
                    && a.Atlases == 0 && b.Atlases == 2, "released routes restore the battle resource owner");
                using (ProfessionAppearanceTextureBindings.Register(a, new[] { guid }, new[] { atlas }))
                {
                    using (ProfessionAppearanceTextureBindings.Register(b, new[] { guid }, new[] { atlas }))
                        ProfessionAppearanceTextureBindings.Required.GetAtlasMaterial(atlas, 0);
                    ProfessionAppearanceTextureBindings.Required.GetAtlasMaterial(atlas, 0);
                    Require(a.Atlases == 1 && b.Atlases == 3, "nested route release restores its earlier owner");
                }
                Require(ProfessionAppearanceTextureBindings.Required.GetAtlasMaterial(weapons.Atlas, 0),
                    "body and independent weapon atlas routing coexist");
                bool rejected = false;
                try { ProfessionAppearanceTextureBindings.Required.LoadAssetSyncByGuid<Texture>("unregistered-host-texture-probe"); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && host.Calls == 1 && ProfessionAppearanceTextureBindings.Current == host,
                    "unregistered requests use the unchanged host endpoint");
                Debug.Log("Original player texture route ownership passed: real packaged texture handles, concurrent body/weapon atlases, nested/out-of-order/idempotent release and unchanged host fallback. Actual player actors use registered routes for the following Play Mode probes.");
            }
            finally
            {
                first.Dispose(); second?.Dispose(); heldA?.Release(); heldB?.Release();
            }
        }
    }
}

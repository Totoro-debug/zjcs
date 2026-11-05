using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        private static IEnumerator CheckPlayerResourceService(AppearanceConfig config, ProfessionAppearanceResolver resolver)
        {
            var previous = ProfessionAppearanceTextureBindings.Current;
            using var first = new OfflineProfessionBattlePlayerResources();
            using var second = new OfflineProfessionBattlePlayerResources();
            var hosts = new[] { new GameObject("First battle player resource owner"), new GameObject("Second battle player resource owner") };
            var cameraObject = new GameObject("Simultaneous battle player visibility camera");
            var target = new RenderTexture(384, 512, 24);
            try
            {
                foreach (var host in hosts) host.AddComponent<SortingGroup>().sortingLayerID = 1182434123;
                hosts[0].transform.position = Vector3.left;
                hosts[1].transform.position = Vector3.right;
                var profiles = new[] { Profile(Sex.Male), Profile(Sex.Female) };
                var snapshots = profiles.Select(profile => JsonUtility.ToJson(profile)).ToArray();
                var a = first.Create(hosts[0].transform, 31, () => profiles[0], _ => 200);
                var b = second.Create(hosts[1].transform, 32, () => profiles[1], _ => 200);
                var roots = new[] { a.Actor.Root, b.Actor.Root };
                var weapons = roots.SelectMany(root => root.GetComponentsInChildren<ProfessionNativeSceneWeaponRenderer>(true)).ToArray();
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
                camera.aspect = .75f; camera.orthographicSize = 3;
                camera.transform.position = new Vector3(0, 1, -100); camera.targetTexture = target;
                for (int frame = 0; frame < 12; frame++)
                { first.Update(); second.Update(); yield return null; }
                Require(ReferenceEquals(previous, ProfessionAppearanceTextureBindings.Current), "simultaneous battle resources preserve the host texture endpoint");
                var renderers = b.Actor.Root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
                Require(renderers.SelectMany(r => r.sharedMaterials).Where(m => m).All(m => m.mainTexture
                    && !m.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal)), "second live battle has actual body and weapon textures");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Capture(bounds, "player-resources-second-before-first-release");
                float time = b.Actor.Renderer.PlayerPhysicsTime;
                first.Dispose(); first.Dispose(); a.Dispose();
                Require(b.Actor.Renderer.PlayerPhysicsTime == time && b.Actor.Root.activeInHierarchy,
                    "out-of-order owner release leaves the other live actor and its physical time intact");
                Require(renderers.SelectMany(r => r.sharedMaterials).Where(m => m).All(m => m.mainTexture),
                    "releasing the first owner keeps every second-owner material texture alive");
                Capture(bounds, "player-resources-second-after-first-release");
                RequireThrowsDisposed(() => first.Update());
                RequireThrowsDisposed(() => first.Create(hosts[0].transform, 31, () => profiles[0], _ => 200));
                RequireThrowsDisposed(a.RefreshAppearance);
                b.RefreshAppearance(); second.Update();
                for (int frame = 0; frame < 6; frame++) { second.Update(); yield return null; }
                Capture(bounds, "player-resources-second-refreshed");
                second.Dispose(); second.Dispose(); b.Dispose();
                yield return null;
                Require(roots.All(root => !root) && weapons.All(weapon => !weapon), "owner disposal releases all actual actors and pooled native weapons");
                Require(ReferenceEquals(previous, ProfessionAppearanceTextureBindings.Current), "all battle resource routes release without replacing the host endpoint");
                Require(profiles.Select(profile => JsonUtility.ToJson(profile)).SequenceEqual(snapshots), "resource service never changes either supplied profile");
                Debug.Log("Original battle player resource service passed: actual profile projection, four actor/death/revival cases, two simultaneous owners, out-of-order release, live texture preservation, refresh, frame-only cache updates, idempotent disposal and unchanged profiles.");
            }
            finally
            {
                if (cameraObject.TryGetComponent<Camera>(out var camera)) camera.targetTexture = null;
                first.Dispose(); second.Dispose();
                Object.Destroy(cameraObject); Object.Destroy(target);
                foreach (var host in hosts) Object.Destroy(host);
            }

            OfflinePlayerProgress Profile(Sex sex)
            {
                var profile = new OfflinePlayerProgress { InitialWeaponType = 200 };
                profile.Wardrobe.Sex = (int)sex; profile.Wardrobe.Profession = 1;
                foreach (var pair in resolver.BuildDefaultAppearanceData(config, sex, (Profession)1).SkinMap)
                    profile.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                return profile;
            }
        }

        private static void RequireThrowsDisposed(Action action)
        {
            try { action(); }
            catch (ObjectDisposedException) { return; }
            throw new InvalidOperationException("Disposed battle player resources must reject new work.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Presentation.Home
{
    [Serializable]
    public sealed class HomePlayerRuntimeData
    {
        public float[] position;
        public float[] scale;
        public float rotation_z;
        public string sorting_layer_name;
        public int sorting_layer;
        public int sorting_order;
        public string animation;
    }

    public sealed class HomePlayerActorView : IDisposable
    {
        private GameObject actor;
        private readonly CharacterCreationMeshFramePlayer animation;
        private bool disposed;

        public Transform Transform => actor == null ? null : actor.transform;

        public HomePlayerActorView(HomeSceneView scene, string runtimePath, OfflineWardrobeProgress wardrobe,
            OfflineCharacterProgressionService progression, CharacterCreationAssetCatalog creationAssets,
            CharacterCreationOfflineService creationService)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (wardrobe == null) throw new ArgumentNullException(nameof(wardrobe));
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            if (creationAssets == null) throw new ArgumentNullException(nameof(creationAssets));
            if (creationService == null) throw new ArgumentNullException(nameof(creationService));

            HomePlayerRuntimeData runtime = JsonUtility.FromJson<HomePlayerRuntimeData>(File.ReadAllText(runtimePath));
            if (runtime == null || runtime.position == null || runtime.position.Length != 3
                || runtime.scale == null || runtime.scale.Length != 3
                || runtime.sorting_layer_name != "Mansion_Front01"
                || runtime.animation != "home/idle_01")
                throw new InvalidDataException("Official home player runtime data is missing or unsupported.");

            actor = new GameObject("HomePlayer");
            try
            {
                actor.transform.SetParent(scene.Root.transform, false);
                actor.transform.localPosition = new Vector3(runtime.position[0], runtime.position[1], runtime.position[2]);
                actor.transform.localScale = new Vector3(runtime.scale[0], runtime.scale[1], runtime.scale[2]);
                actor.transform.localRotation = Quaternion.Euler(0f, 0f, runtime.rotation_z);
                SortingGroup sorting = actor.AddComponent<SortingGroup>();
                sorting.sortingLayerName = "Default";
                sorting.sortingOrder = scene.GetNextSortingOrder(runtime.sorting_layer, runtime.sorting_order);

                animation = actor.AddComponent<CharacterCreationMeshFramePlayer>();
                animation.Initialize(wardrobe.Sex == 1 ? "female" : "male");
                ApplyAppearance(wardrobe, progression, creationAssets, creationService);
            }
            catch
            {
                if (actor != null) UnityEngine.Object.Destroy(actor);
                throw;
            }
        }

        private void ApplyAppearance(OfflineWardrobeProgress wardrobe,
            OfflineCharacterProgressionService progression, CharacterCreationAssetCatalog creationAssets,
            CharacterCreationOfflineService creationService)
        {
            WardrobeResolution appearance = progression.ResolveAppearance();
            var selected = new List<CharacterCreationSkinInfo>();
            foreach (KeyValuePair<int, int> entry in appearance.SkinByType.OrderBy(value => value.Key))
            {
                if (!creationAssets.TryGetSkinInfo(entry.Value, out CharacterCreationSkinInfo info)
                    || info.Type != entry.Key)
                    throw new InvalidDataException("The saved home player skin is missing: " + entry.Value);
                selected.Add(info);
            }

            var draft = new CharacterAppearanceDraft
            {
                body_type = wardrobe.Sex == 1 ? "female" : "male",
                skin_color = DyeHex(wardrobe, 1),
                hair_color = DyeHex(wardrobe, 2),
                eye_color = DyeHex(wardrobe, 3),
            };
            var changes = new Dictionary<int, Vector4[]>();
            foreach (CharacterCreationSkinInfo info in selected)
                if (info.HasDye)
                    changes.Add(info.Type, creationService.GetDyeHsvChanges(draft, info.Type));

            animation.ApplySkins(selected.Select(info => info.Name));
            animation.ApplyDye(changes);
        }

        private static string DyeHex(OfflineWardrobeProgress wardrobe, int type)
        {
            OfflineAppearanceDye dye = wardrobe.Dyes?.FirstOrDefault(value => value.Type == type);
            if (dye == null) return "808080";
            Color color = Color.HSVToRGB((dye.Hue % 360) / 360f,
                Mathf.Clamp01(dye.Saturation / 100f), Mathf.Clamp01(dye.Value / 100f));
            return ColorUtility.ToHtmlStringRGB(color);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (actor != null) UnityEngine.Object.Destroy(actor);
            actor = null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.CharacterCreation
{
    public static class CharacterCreationNativeModelFactory
    {
        private const string SkinVariantPrefix = "__skin__";
        private static readonly int DyeTextureId = Shader.PropertyToID("_DyeTex");
        private static readonly int DyeHsv1Id = Shader.PropertyToID("_DyeHsv1");
        private static readonly int DyeHsv2Id = Shader.PropertyToID("_DyeHsv2");
        private static readonly int DyeHsv3Id = Shader.PropertyToID("_DyeHsv3");

        public static GameObject CreateWorldModel(string slotId, string persistentDataPath, out string characterName)
        {
            return CreateModel(slotId, persistentDataPath, null, true, out characterName);
        }

        public static GameObject CreatePreviewModel(string slotId, string persistentDataPath,
            WardrobeResolution appearance)
        {
            if (appearance == null) throw new ArgumentNullException(nameof(appearance));
            return CreateModel(slotId, persistentDataPath, appearance, false, out _);
        }

        public static GameObject CreateArenaGuardModel(int sex, int profession,
            IReadOnlyDictionary<int, int> appendAppearances)
        {
            CharacterCreationAssetCatalog assets = CharacterCreationAssetCatalog.LoadDefault();
            GameObject prefab = sex == 1 ? assets.FemaleNativeModelPrefab : assets.MaleNativeModelPrefab;
            if (prefab == null) throw new InvalidOperationException("Official native character model is not installed.");

            var skinByType = new Dictionary<int, int>();
            foreach (CharacterCreationSkinInfo skin in assets.GetDefaultSkinInfos(sex, profession))
                skinByType[skin.Type] = skin.Id;
            foreach (KeyValuePair<int, int> appearance in appendAppearances ??
                         new Dictionary<int, int>())
            {
                if (!assets.TryGetSkinInfo(appearance.Value, out CharacterCreationSkinInfo skin)
                    || skin.Type != appearance.Key)
                    throw new InvalidOperationException("Official arena guard appearance is invalid: "
                        + appearance.Key + "/" + appearance.Value);
                skinByType[appearance.Key] = appearance.Value;
            }

            GameObject model = UnityEngine.Object.Instantiate(prefab);
            model.name = "ArenaGuardCharacter";
            try
            {
                var activeSkins = new HashSet<string>(StringComparer.Ordinal);
                foreach (int skinId in skinByType.Values)
                {
                    if (!assets.TryGetSkinInfo(skinId, out CharacterCreationSkinInfo skin))
                        throw new InvalidOperationException("Official arena guard skin is missing: " + skinId);
                    activeSkins.Add(skin.Name);
                }

                foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.name.StartsWith(SkinVariantPrefix, StringComparison.Ordinal)) continue;
                    child.gameObject.SetActive(activeSkins.Contains(child.name.Substring(SkinVariantPrefix.Length)));
                }

                Animator animator = model.GetComponent<Animator>();
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("Official native character model has no Animator controller.");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                model.AddComponent<CharacterNativeAttachmentGeometry>().Initialize(sex == 1 ? "female" : "male");
                return model;
            }
            catch
            {
                UnityEngine.Object.Destroy(model);
                throw;
            }
        }

        private static GameObject CreateModel(string slotId, string persistentDataPath,
            WardrobeResolution appearanceOverride, bool requireWorldEntry, out string characterName)
        {
            var slots = new ProfileSlotCatalog(persistentDataPath);
            OfflineMainlineProgressSession session = requireWorldEntry
                ? OfflineMainlineProgressSession.OpenCharacter(slots, slotId)
                : OfflineMainlineProgressSession.InspectCharacter(slots, slotId);
            if (!session.Save.Player.CharacterCreated || requireWorldEntry && session.Save.Player.InitialWeaponType == 0)
                throw new InvalidOperationException("Finish character creation and weapon selection before entering Map10.");

            CharacterCreationAssetCatalog creationAssets = CharacterCreationAssetCatalog.LoadDefault();
            OfficialCharacterAssetCatalog characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
            var progression = new OfflineCharacterProgressionService(session, characterAssets.Load());
            int sex = session.Save.Player.Wardrobe.Sex;
            WardrobeResolution appearance = appearanceOverride ?? progression.ResolveAppearance();
            GameObject prefab = sex == 1 ? creationAssets.FemaleNativeModelPrefab : creationAssets.MaleNativeModelPrefab;
            if (prefab == null) throw new InvalidOperationException("Official native character model is not installed.");

            GameObject model = UnityEngine.Object.Instantiate(prefab);
            model.name = "PlayerCharacter_" + slotId;
            try
            {
                var activeSkins = new HashSet<string>(StringComparer.Ordinal);
                foreach (int skinId in appearance.SkinByType.Values)
                    if (creationAssets.TryGetSkinInfo(skinId, out CharacterCreationSkinInfo info))
                        activeSkins.Add(info.Name);

                foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.name.StartsWith(SkinVariantPrefix, StringComparison.Ordinal)) continue;
                    child.gameObject.SetActive(activeSkins.Contains(child.name.Substring(SkinVariantPrefix.Length)));
                }

                var appearanceDraft = new CharacterAppearanceDraft
                {
                    body_type = sex == 1 ? "female" : "male",
                    skin_color = DyeHex(session.Save.Player.Wardrobe, 1),
                    hair_color = DyeHex(session.Save.Player.Wardrobe, 2),
                    eye_color = DyeHex(session.Save.Player.Wardrobe, 3)
                };
                var creationService = new CharacterCreationOfflineService(slots, creationAssets, characterAssets);
                var channelsByType = new Dictionary<int, Vector4[]>();
                foreach (int skinType in appearance.SkinByType.Keys)
                    channelsByType[skinType] = creationService.GetDyeHsvChanges(appearanceDraft, skinType);
                ApplyDyeChannels(model, channelsByType);

                Animator animator = model.GetComponent<Animator>();
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("Official native character model has no Animator controller.");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                characterName = session.Save.Player.CharacterName;
                model.AddComponent<CharacterNativeAttachmentGeometry>().Initialize(sex == 1 ? "female" : "male");
                return model;
            }
            catch
            {
                UnityEngine.Object.Destroy(model);
                throw;
            }
        }

        private static string DyeHex(OfflineWardrobeProgress wardrobe, int type)
        {
            OfflineAppearanceDye dye = wardrobe.Dyes.Find(value => value.Type == type);
            if (dye == null) return "808080";
            Color color = Color.HSVToRGB((dye.Hue % 360) / 360f,
                Mathf.Clamp01(dye.Saturation / 100f), Mathf.Clamp01(dye.Value / 100f));
            return ColorUtility.ToHtmlStringRGB(color);
        }

        private static void ApplyDyeChannels(GameObject model, IReadOnlyDictionary<int, Vector4[]> channelsByType)
        {
            var properties = new MaterialPropertyBlock();
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                CharacterCreationDyeMapBinding binding = child.GetComponent<CharacterCreationDyeMapBinding>();
                Renderer renderer = child.GetComponent<Renderer>();
                if (binding == null || renderer == null) continue;
                channelsByType.TryGetValue(binding.SkinType, out Vector4[] channels);
                properties.Clear();
                properties.SetTexture(DyeTextureId, binding.DyeMask != null ? binding.DyeMask : Texture2D.blackTexture);
                properties.SetVector(DyeHsv1Id, Channel(channels, 0));
                properties.SetVector(DyeHsv2Id, Channel(channels, 1));
                properties.SetVector(DyeHsv3Id, Channel(channels, 2));
                renderer.SetPropertyBlock(properties);
            }
        }

        private static Vector4 Channel(Vector4[] channels, int index)
        {
            return channels != null && index < channels.Length ? channels[index] : Vector4.zero;
        }
    }
}

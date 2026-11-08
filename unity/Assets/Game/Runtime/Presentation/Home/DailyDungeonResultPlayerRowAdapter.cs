using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Character;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonResultPlayerRowAdapter : MonoBehaviour
    {
        private const string DefaultSpineMaterialPath =
            "Dungeons/UI/884_156692/Materials/Spine/SkeletonGraphicDefault";
        private const string DyeSpineMaterialPath =
            "Dungeons/UI/884_156692/Materials/Spine/SkeletonGraphicDye";
        private readonly Dictionary<Graphic, Material> originalSpineMaterials =
            new Dictionary<Graphic, Material>();
        private readonly Dictionary<Graphic, Material> runtimeSpineMaterials =
            new Dictionary<Graphic, Material>();
        public void Configure(HomeFeatureRuntime host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            BindRuntimeSpineMaterials();

            TMP_Text name = FindNamedComponent<TMP_Text>("PlayerName");
            if (name != null)
                name.text = string.IsNullOrWhiteSpace(host.CharacterName) ? "本地玩家" : host.CharacterName;

            var portrait = OfficialBattleCharacterPortraits.ForProfile(gameObject,
                host.DailyDungeonProgression, host.ActiveProfileSlotId, host.SavesRoot);
            Sprite playerPortrait = portrait(new FightBattleUnitState { Id = 1, CharacterType = 1 });
            if (playerPortrait == null)
                throw new InvalidOperationException("Official daily dungeon player portrait is unavailable.");

            bool bound = false;
            foreach (Image image in GetComponentsInChildren<Image>(true))
            {
                if (!string.Equals(image.gameObject.name, "HeadImg", StringComparison.Ordinal)) continue;
                image.sprite = playerPortrait;
                image.color = Color.white;
                image.preserveAspect = true;
                bound = true;
            }
            foreach (RawImage image in GetComponentsInChildren<RawImage>(true))
            {
                if (!string.Equals(image.gameObject.name, "HeadImg", StringComparison.Ordinal)) continue;
                image.texture = playerPortrait.texture;
                image.color = Color.white;
                bound = true;
            }
            if (!bound)
            {
                throw new InvalidOperationException("Official daily dungeon player portrait slot is missing.");
            }

            Transform thumbsUp = FindNamedTransform("FightThumbsUpUI");
            if (thumbsUp != null) thumbsUp.gameObject.SetActive(false);
            foreach (Button button in GetComponentsInChildren<Button>(true))
                button.interactable = false;
        }

        private void BindRuntimeSpineMaterials()
        {
            Material defaultTemplate = Resources.Load<Material>(DefaultSpineMaterialPath);
            Material dyeTemplate = Resources.Load<Material>(DyeSpineMaterialPath);
            if (defaultTemplate == null || defaultTemplate.shader == null
                || dyeTemplate == null || dyeTemplate.shader == null)
                throw new InvalidOperationException("Official daily dungeon Windows Spine materials are missing.");

            int boundCount = 0;
            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (runtimeSpineMaterials.TryGetValue(graphic, out Material existing))
                {
                    graphic.material = existing;
                    boundCount++;
                    continue;
                }

                Material source = graphic.material;
                if (source == null || source.shader == null) continue;
                Material template = source.shader.name == "Spine/SkeletonGraphicDye"
                    ? dyeTemplate : source.shader.name == "Spine/SkeletonGraphic" ? defaultTemplate : null;
                if (template == null) continue;
                if (!template.shader.isSupported)
                    throw new InvalidOperationException("Daily dungeon Spine shader is unsupported: "
                        + template.shader.name);
                Material runtimeMaterial = new Material(source)
                {
                    name = source.name + "_WindowsRuntime",
                    hideFlags = HideFlags.DontSave,
                    shader = template.shader,
                    renderQueue = source.renderQueue
                };
                originalSpineMaterials.Add(graphic, source);
                runtimeSpineMaterials.Add(graphic, runtimeMaterial);
                graphic.material = runtimeMaterial;
                graphic.SetMaterialDirty();
                boundCount++;
            }
            if (boundCount == 0)
                throw new InvalidOperationException("Official daily dungeon Spine atlas materials are missing.");
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<Graphic, Material> pair in runtimeSpineMaterials)
            {
                if (pair.Key != null && pair.Key.material == pair.Value)
                    pair.Key.material = originalSpineMaterials[pair.Key];
                if (pair.Value != null) Destroy(pair.Value);
            }
            runtimeSpineMaterials.Clear();
            originalSpineMaterials.Clear();
        }

        private T FindNamedComponent<T>(string nodeName) where T : Component
        {
            foreach (T component in GetComponentsInChildren<T>(true))
                if (component != null && string.Equals(component.gameObject.name, nodeName, StringComparison.Ordinal))
                    return component;
            return null;
        }

        private Transform FindNamedTransform(string nodeName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child != null && string.Equals(child.name, nodeName, StringComparison.Ordinal)) return child;
            return null;
        }
    }
}

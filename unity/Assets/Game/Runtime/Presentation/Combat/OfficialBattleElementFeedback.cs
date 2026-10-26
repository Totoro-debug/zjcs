using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialBattleElementFeedback
    {
        public static ElementConfrontationUI Bind(int effectType, Dictionary<string,GameObject> nodes,
            OfficialBattleHudAnimationPlayer animations, out BattleElementFeedbackBinding binding)
        {
            binding = OfficialBattleElementFeedbackCatalog.Find(effectType);
            if (binding == null) return null;
            string resource = OfficialBattleElementFeedbackCatalog.ResourceRoot;
            foreach (var source in binding.texts) BindText(source,nodes);
            foreach (var source in binding.materials)
            {
                var renderer = nodes[source.path].GetComponent<SpriteRenderer>();
                renderer.sprite = Required<Sprite>(resource+source.sprite);
                renderer.sharedMaterial = Required<Material>(resource+source.material);
                renderer.sortingOrder = source.order;
                renderer.drawMode = (SpriteDrawMode)source.drawMode;
                renderer.size = new Vector2(source.size[0],source.size[1]);
                renderer.tileMode = (SpriteTileMode)source.tileMode;
            }
            Action refreshDescribe = null;
            foreach (var source in binding.backgrounds)
            {
                var background = nodes[source.backgroundPath].AddComponent<DamageAutoSizeTextBack>();
                background.Bind(nodes[source.backgroundPath].GetComponent<SpriteRenderer>(),
                    nodes[source.textPath].GetComponent<TMP_Text>(),source.align,source.paddingX,source.paddingY,
                    source.minWidth,source.maxWidth,source.minHeight,source.maxHeight);
                background.OnRefresh();
                if (source.textPath == binding.describePath) refreshDescribe += background.OnRefresh;
            }
            var sourceBinding = binding;
            var view = nodes[binding.elementPath].AddComponent<ElementConfrontationUI>();
            view.Bind(nodes[binding.effectPaths[0]].GetComponent<SpriteRenderer>(),
                nodes[binding.effectPaths[1]].GetComponent<SpriteRenderer>(),
                nodes[binding.effectPaths[2]].GetComponent<SpriteRenderer>(),
                nodes[binding.bgPath],nodes[binding.describePath].GetComponent<TMP_Text>(),
                binding.keys[0].text,binding.keys[1].text,binding.keys[2].text,binding.keys[3].text,
                trigger =>
                {
                    foreach (var transition in sourceBinding.triggers)
                        if (transition.trigger == trigger)
                        {
                            if (!animations.Play(sourceBinding.elementPath,transition.clip))
                                throw new InvalidOperationException("Original element animation is missing: "+transition.clip);
                            return;
                        }
                    throw new InvalidOperationException("Original element transition is missing: "+trigger);
                },refreshDescribe);
            return view;
        }

        public static TextMeshPro BindText(BattleElementTextBinding source, Dictionary<string,GameObject> nodes)
        {
            string resource=OfficialBattleElementFeedbackCatalog.ResourceRoot;
            var text=nodes[source.path].GetComponent<TextMeshPro>();
            text.font=Required<TMP_FontAsset>(resource+source.font);
            text.fontSharedMaterial=Required<Material>(resource+source.material);
            text.fontSize=source.size;
            text.fontStyle=(FontStyles)source.style;
            text.fontWeight=(FontWeight)source.weight;
            text.characterSpacing=source.characterSpacing;
            text.wordSpacing=source.wordSpacing;
            text.lineSpacing=source.lineSpacing;
            text.margin=new Vector4(source.margin[0],source.margin[1],source.margin[2],source.margin[3]);
            text.renderer.sortingOrder=source.order;
            text.enableVertexGradient=source.enableGradient;
            if (source.enableGradient)
            {
                var gradient=source.gradient ?? throw new FormatException("Original TMP gradient is missing.");
                text.colorGradient=new VertexGradient(GradientColor(gradient.topLeft),GradientColor(gradient.topRight),
                    GradientColor(gradient.bottomLeft),GradientColor(gradient.bottomRight));
            }
            if (source.effect!=null) ApplyTextEffect(text,source.effect);
            return text;
        }

        private static Color GradientColor(float[] value)
        {
            if (value==null || value.Length!=4) throw new FormatException("Original TMP gradient color is invalid.");
            return new Color(value[0],value[1],value[2],value[3]);
        }

        private static void ApplyTextEffect(TMP_Text text, BattleElementTextEffect effect)
        {
            // APK ARM64 TMPEffect.DoSetup: pointSize / fontSize / atlasPadding,
            // then Clamp01(outlineWidth * factor); callback stores the raw outline color.
            Material material = text.fontMaterial;
            material.SetFloat("_FaceDilate",effect.dilate);
            if (effect.outline)
            {
                material.EnableKeyword("OUTLINE_ON");
                float factor = text.font.faceInfo.pointSize / text.fontSize / text.font.atlasPadding;
                material.SetFloat("_OutlineWidth",Mathf.Clamp01(effect.width * factor));
                material.SetColor("_OutlineColor",new Color(effect.color[0],effect.color[1],effect.color[2],effect.color[3]));
            }
            else
            {
                material.DisableKeyword("OUTLINE_ON");
                material.SetFloat("_OutlineWidth",0f);
                material.SetColor("_OutlineColor",Color.black);
            }
            ShaderUtilities.UpdateShaderRatios(material);
            text.UpdateMeshPadding();
        }

        public static void HideImmuneNumbers(BattleElementFeedbackBinding binding, Dictionary<string,GameObject> nodes)
        {
            foreach (string path in binding.hideObjects) nodes[path].SetActive(false);
            foreach (string path in binding.hideRenderers) nodes[path].GetComponent<Renderer>().enabled=false;
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            var asset = Resources.Load<T>(path);
            if (asset == null) throw new InvalidOperationException("Original element feedback asset is missing: "+path);
            return asset;
        }
    }
}

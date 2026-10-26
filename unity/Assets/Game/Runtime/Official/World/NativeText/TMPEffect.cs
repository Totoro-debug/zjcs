// Reconstructed from pinned APK ARM64 methods; provenance is generated beside this module.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TMPro
{
    public class TMPEffect : MonoBehaviour
    {
        public static bool FixFontSizeIssue = true;
        private static readonly int _FaceDilate = Shader.PropertyToID("_FaceDilate");
        private const string OUTLINE_ON = "OUTLINE_ON";
        private static readonly int _OutlineColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int _OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private const string OUTLINE_ON_2 = "OUTLINE_ON_2";
        private static readonly int _OutlineColor2 = Shader.PropertyToID("_OutlineColor2");
        private static readonly int _OutlineWidth2 = Shader.PropertyToID("_OutlineWidth2");
        private const string UNDERLAY_ON = "UNDERLAY_ON";
        private static readonly int _UnderlayColor = Shader.PropertyToID("_UnderlayColor");
        private static readonly int _UnderlayOffsetX = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int _UnderlayOffsetY = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int _UnderlaySoftness = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int _SrcAlphaMode = Shader.PropertyToID("_SrcAlphaMode");
        private static readonly int _DstAlphaMode = Shader.PropertyToID("_DstAlphaMode");
        private const string GLOW_ON = "GLOW_ON";
        private static readonly int _GlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int _GlowOffset = Shader.PropertyToID("_GlowOffset");
        private static readonly int _GlowInner = Shader.PropertyToID("_GlowInner");
        private static readonly int _GlowOuter = Shader.PropertyToID("_GlowOuter");
        private static readonly int _GlowPower = Shader.PropertyToID("_GlowPower");
        private const string USE_VERTEX_COLOR = "USE_VERTEX_COLOR";
        private const string GRAY_ON = "GRAY_ON";

        public float dilate;
        public bool linearDodge, useVertexColor, RealtimeUpdate, outline;
        public Color outlineColor = Color.black;
        public float outlineWidth;
        public bool outline2;
        public Color outlineColor2 = Color.black;
        public float outlineWidth2;
        private float m_outlineWidth, m_outlineWidth2;
        public bool underlay;
        public Color underlayColor = Color.black;
        public float shadowAngle = 90, shadowDis;
        private float m_underlayOffsetX, m_underlayOffsetY;
        public float shadowBlur;
        private float m_underlaySoftness;
        public bool glow;
        public Color glowColor = Color.black;
        public float glowOffset, glowInner, glowOuter, glowPower;
        public bool gray;
        private TMP_Text m_text;
        private readonly List<TMPEffectMaterial> effectMaterials = new List<TMPEffectMaterial>();

        public TMP_Text text { get { if (m_text == null) m_text = GetComponent<TMP_Text>(); return m_text; } }
        private void OnEnable() { ReleaseAllMats(); DoSetup(true); }
        private void OnDisable() { ReleaseAllMats(); DoSetup(false); }
        private void OnDestroy() { ReleaseAllMats(); }
        private void OnValidate() { bool value = enabled; ReleaseAllMats(); DoSetup(value); }
        private void Update() { if (RealtimeUpdate) { bool value = enabled; ReleaseAllMats(); DoSetup(value); } }
        public void SetupEffect(bool enable) { ReleaseAllMats(); DoSetup(enable); }
        public void SetGray(bool value) { gray = value; ReleaseAllMats(); DoSetup(true); }

        // Native metadata marks this private, but the separate manager calls it
        // directly. Internal visibility repairs that C# access without a new path.
        internal void ReleaseAllMats()
        {
            for (int i = 0; i < effectMaterials.Count; i++) TMPEffectMaterial.ReleaseEffectMaterial(effectMaterials[i]);
            effectMaterials.Clear();
        }
        internal void SetupFromSizeChange() { DoSetup(true); }

        protected virtual void DoSetup(bool enable)
        {
            text.fontSharedMaterial = text.font.material;
            float? fontSize = null;
            if (CanRender())
            {
                text.ForceMeshUpdate(false, false);
                if (enable)
                {
                    Material original = text.fontSharedMaterial;
                    fontSize = text.fontSize;
                    float factor = text.font.faceInfo.pointSize / fontSize.Value / text.font.atlasPadding;
                    if (outline)
                    {
                        m_outlineWidth = Mathf.Clamp01(factor * outlineWidth);
                        if (outline2) m_outlineWidth2 = Mathf.Clamp01(factor * outlineWidth2);
                    }
                    else outline2 = false;
                    if (underlay)
                    {
                        m_underlaySoftness = Mathf.Clamp01(factor * shadowBlur);
                        float distance = Mathf.Clamp01(factor * shadowDis);
                        float angle = shadowAngle * 0.01745329238474369f;
                        m_underlayOffsetX = -Mathf.Clamp(Mathf.Cos(angle) * distance, -1, 1);
                        m_underlayOffsetY = -Mathf.Clamp(Mathf.Sin(angle) * distance, -1, 1);
                    }
                    TMPEffectMaterial effect = TMPEffectMaterial.LoadEffectMaterial(original, ApplyMaterial);
                    effectMaterials.Add(effect);
                    text.fontSharedMaterial = effect.EffectMaterial;
                    text.ForceMeshUpdate(false, false);
                }
            }
            if (FixFontSizeIssue)
            {
                if (fontSize.HasValue)
                {
                    TMPEffectManager.Instance.RegisterEffect(this, fontSize.Value);
                    if (!Zjcs.Official.World.NativeText.NativeTmpMaterialAccess.IsDirty(text) && CanvasUpdateRegistry.IsRebuildingGraphics())
                        Zjcs.Official.World.NativeText.NativeTmpMaterialAccess.SetDirty(text);
                }
                else TMPEffectManager.Instance.UnregisterEffect(this);
            }
        }

        private bool CanRender()
        {
            return text != null && text.gameObject.activeInHierarchy && text.enabled && text.textInfo != null &&
                text.font != null && text.font.material != null && enabled;
        }

        private void ApplyMaterial(Material material)
        {
            material.SetFloat(_FaceDilate, dilate);
            if (useVertexColor) material.EnableKeyword(USE_VERTEX_COLOR); else material.DisableKeyword(USE_VERTEX_COLOR);
            if (outline) material.EnableKeyword(OUTLINE_ON); else material.DisableKeyword(OUTLINE_ON);
            material.SetColor(_OutlineColor, outline ? outlineColor : Color.black);
            material.SetFloat(_OutlineWidth, outline ? m_outlineWidth : 0);
            if (outline2) material.EnableKeyword(OUTLINE_ON_2); else material.DisableKeyword(OUTLINE_ON_2);
            material.SetColor(_OutlineColor2, outline2 ? outlineColor2 : Color.black);
            material.SetFloat(_OutlineWidth2, outline2 ? m_outlineWidth2 : 0);
            if (underlay) material.EnableKeyword(UNDERLAY_ON); else material.DisableKeyword(UNDERLAY_ON);
            material.SetColor(_UnderlayColor, underlay ? underlayColor : Color.black);
            material.SetFloat(_UnderlaySoftness, underlay ? m_underlaySoftness : 0);
            material.SetFloat(_UnderlayOffsetX, underlay ? m_underlayOffsetX : 0);
            material.SetFloat(_UnderlayOffsetY, underlay ? m_underlayOffsetY : 0);
            if (glow) material.EnableKeyword(GLOW_ON); else material.DisableKeyword(GLOW_ON);
            material.SetColor(_GlowColor, glow ? glowColor : Color.black);
            material.SetFloat(_GlowOffset, glow ? glowOffset : 0);
            material.SetFloat(_GlowInner, glow ? glowInner : 0);
            material.SetFloat(_GlowOuter, glow ? glowOuter : 0);
            material.SetFloat(_GlowPower, glow ? glowPower : 0);
            if (gray) material.EnableKeyword(GRAY_ON); else material.DisableKeyword(GRAY_ON);
            material.SetInt(_SrcAlphaMode, 1);
            material.SetInt(_DstAlphaMode, linearDodge ? 1 : 10);
        }
    }

    internal sealed class TMPEffectManager
    {
        public static readonly TMPEffectManager Instance = new TMPEffectManager();
        private readonly Dictionary<int, float> mEffectFontSizes = new Dictionary<int, float>();
        private TMPEffectManager() { TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged); }
        public void RegisterEffect(TMPEffect effect, float fontSize) { mEffectFontSizes[effect.GetInstanceID()] = fontSize; }
        public void UnregisterEffect(TMPEffect effect) { mEffectFontSizes.Remove(effect.GetInstanceID()); }
        private void OnTextChanged(UnityEngine.Object value)
        {
            if (!(value is TMP_Text text) || !text.enableAutoSizing || !text.TryGetComponent(out TMPEffect effect) ||
                !mEffectFontSizes.TryGetValue(effect.GetInstanceID(), out float size) || size == text.fontSize || !(effect.outline || effect.underlay)) return;
            mEffectFontSizes[effect.GetInstanceID()] = text.fontSize;
            effect.ReleaseAllMats(); effect.SetupFromSizeChange();
        }
    }

    public class TMPEffectMaterial
    {
        public Material EffectMaterial { get; private set; }
        public int ID { get; private set; }
        public int ShaderID { get; private set; }
        private static readonly Dictionary<int, Material> m_refMatDic = new Dictionary<int, Material>();
        private static readonly Dictionary<int, Dictionary<int, TMPEffectMaterial>> pool = new Dictionary<int, Dictionary<int, TMPEffectMaterial>>();
        private int refCount = 1;
        private TMPEffectMaterial(Material material, int id, int shaderId) { EffectMaterial = material; ID = id; ShaderID = shaderId; }
        public static TMPEffectMaterial LoadEffectMaterial(Material original, Action<Material> setup)
        {
            int shader = original.shader.GetHashCode();
            if (m_refMatDic.ContainsKey(shader))
            {
                if (m_refMatDic[shader] == null) m_refMatDic[shader] = new Material(original);
            }
            else m_refMatDic.Add(shader, new Material(original));
            Material reference = m_refMatDic[shader];
            reference.CopyPropertiesFromMaterial(original);
            setup?.Invoke(reference);
            int id = unchecked((int)reference.ComputeCRC());
            if (!pool.ContainsKey(shader)) pool.Add(shader, new Dictionary<int, TMPEffectMaterial>());
            Dictionary<int, TMPEffectMaterial> entries = pool[shader];
            if (entries.ContainsKey(id))
            {
                if (entries[id].EffectMaterial == null) entries[id].EffectMaterial = new Material(reference) { name = original.name };
                entries[id].refCount++;
                return entries[id];
            }
            var material = new Material(reference) { name = original.name };
            var result = new TMPEffectMaterial(material, id, shader);
            entries.Add(id, result);
            return result;
        }
        public static void ReleaseEffectMaterial(TMPEffectMaterial effect)
        {
            if (--pool[effect.ShaderID][effect.ID].refCount > 0) return;
            DestroyMaterial(pool[effect.ShaderID][effect.ID].EffectMaterial);
            pool[effect.ShaderID].Remove(effect.ID);
            if (pool[effect.ShaderID].Count != 0) return;
            DestroyMaterial(m_refMatDic[effect.ShaderID]); m_refMatDic.Remove(effect.ShaderID); pool.Remove(effect.ShaderID);
        }
        private static void DestroyMaterial(Material material)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(material); else UnityEngine.Object.DestroyImmediate(material);
        }
    }
}

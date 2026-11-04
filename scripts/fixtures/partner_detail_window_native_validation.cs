using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.WorldUI;

// Import/reference validation followed by the existing graphical panel probe.
// This does not initialize the new detail page or certify its presentation.
public static class PartnerDetailWindowNativeValidation
{
    public static void Run()
    {
        var prefab = OfficialPartnerDetailWindowAssetCatalog.Load().PanelPrefab;
        foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                "Missing original script on " + transform.name);
        Require(!SerializationUtility.HasManagedReferencesWithMissingTypes(prefab.GetComponent<NpcDetailPanel>()),
            "Detail window managed state types resolve");
        var panel = prefab.GetComponent<NpcDetailPanel>();
        foreach (string name in new[] { "mPropContent", "mGiftContent", "mFriendshipContent", "mDetailContent",
            "mProfileContent", "mAchievementPanel", "mTabGroup", "mLeftBtn", "mRightBtn", "mFullViewBtn", "mExitFullViewBtn", "mNpcTreasureIcon" })
            Require(Get(panel, name) is UnityEngine.Object value && value, "Original detail reference: " + name);
        foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            Require(!SerializationUtility.HasManagedReferencesWithMissingTypes(component),
                "Managed state binding on " + component.GetType().Name + "/" + component.name);
        VerifyCurve("NpcDetailPanel_TalkGo_In", typeof(CanvasGroup), "m_Alpha");
        VerifyCurve("HpSlider", typeof(Image), "m_Color.a");
        Debug.Log("PARTNER_DETAIL_NATIVE_REFERENCES_OK");
        PartnerPanelHostValidation.Run();
    }
    static object Get(object owner, string name) => owner.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static void VerifyCurve(string name, Type type, string property)
    {
        var clips = AssetDatabase.FindAssets(name + " t:AnimationClip", new[] { "Assets/Official/Partners/UI/DetailWindow" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
            .Where(clip => clip.name == name).ToArray();
        Require(clips.Length == 1, "Unique recovered root clip: " + name);
        var clip = clips[0];
        var binding = AnimationUtility.GetCurveBindings(clip).Single(row =>
            row.path == "" && row.type == type && row.propertyName == property);
        var curve = AnimationUtility.GetEditorCurve(clip, binding);
        Require(curve != null && curve.keys.Length > 1, "Original root curve is readable: " + name);
        var go = new GameObject("Actual legacy root animation target", typeof(RectTransform), type);
        try
        {
            foreach (float time in new[] { curve.keys.First().time, curve.keys.Last().time,
                (curve.keys.First().time + curve.keys.Last().time) * .5f })
            {
                clip.SampleAnimation(go, time);
                float actual = type == typeof(CanvasGroup) ? go.GetComponent<CanvasGroup>().alpha : go.GetComponent<Image>().color.a;
                Require(Mathf.Abs(actual - curve.Evaluate(time)) < .0001f, "Restored legacy root curve samples on its actual Unity component: " + name);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

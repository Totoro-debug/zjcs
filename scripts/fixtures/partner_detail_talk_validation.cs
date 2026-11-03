using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryBubbles;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

// Exercise the native talk region itself. This does not claim a touch-button
// interaction: the complete detail controller still needs its visual bindings.
public static class PartnerDetailTalkValidation
{
    public static IEnumerator Check(Transform parent, Camera camera, TMP_FontAsset font)
    {
        var active = parent.Cast<Transform>().Where(child => child.gameObject.activeSelf).ToArray();
        foreach (var child in active) child.gameObject.SetActive(false);
        var assets = OfficialPartnerDetailTalkAssetCatalog.Load();
        var oldClear = camera.clearFlags;
        camera.clearFlags = CameraClearFlags.SolidColor;
        var oldText = WorldMultiLanguageText.Localize;
        var oldFont = WorldMultiLanguageText.LanguageFont;
        WorldMultiLanguageText.Localize = key => assets.Localize(key);
        WorldMultiLanguageText.LanguageFont = () => font;
        var root = Object.Instantiate(assets.TalkPrefab, parent, false);
        try
        {
            var talk = root.GetComponent<NpcTalkUI>();
            Require(talk && root.GetComponentsInChildren<MonoBehaviour>(true).All(component => component), "original scripts present");
            var type = root.GetComponentInChildren<TMP_TypeWriterEffect>(true);
            var text = root.GetComponentInChildren<TMP_Text>(true);
            var animation = root.GetComponent<Animation>();
            Require(type && text && animation, "original text/effect/legacy Animation components");
            Require(animation.GetClip("NpcDetailPanel_TalkGo_In") && animation.GetClip("NpcDetailPanel_TalkGo_Loop") &&
                animation.GetClip("NpcDetailPanel_TalkGo_Out"), "all three original clips bound");
            talk.SetStateSwitcher(false);
            Require(!text.gameObject.activeSelf, "original hidden state");
            talk.ShowNpcTalk("npc_talk_1");
            Require(type.IsPlaying && text.maxVisibleCharacters < text.text.Length, "original typewriter started");
            Require(State(talk) == 0 && animation.IsPlaying("NpcDetailPanel_TalkGo_In"), "original start state and clip");
            yield return new WaitForSeconds(.18f);
            Capture(camera, "detail-talk-typing.png");
            int visible = text.maxVisibleCharacters;
            yield return new WaitForSeconds(.4f);
            Require(text.maxVisibleCharacters > visible, "typewriter advances");
            talk.ShowNpcTalk("npc_talk_2");
            Require(State(talk) == 0 && type.IsPlaying && text.text == assets.Localize("npc_talk_2"), "retrigger replaces original text and restarts");
            yield return new WaitForSeconds(2f);
            Require(!type.IsPlaying && text.maxVisibleCharacters == int.MaxValue && State(talk) != 2,
                "completed text waits original two-second close delay");
            Capture(camera, "detail-talk-visible.png");
            yield return new WaitForSeconds(2.2f);
            Require(State(talk) == 2 && root.GetComponent<CanvasGroup>().alpha == 0, "original end state and fade-out");
            Capture(camera, "detail-talk-ended.png");
            talk.ShowNpcTalk("npc_talk_1");
            yield return new WaitForSeconds(2f);
            root.SetActive(false); yield return new WaitForSeconds(2.1f); root.SetActive(true);
            Require(State(talk) != 2, "disable cancels pending delayed end");
            talk.ShowNpcTalk("npc_talk_3");
            yield return new WaitForSeconds(.15f); root.SetActive(false);
            Require(!type.IsPlaying, "disable interrupts typewriter through original child lifecycle");
            root.SetActive(true); talk.ShowNpcTalk("npc_talk_1");
            yield return new WaitForSeconds(2f);
            Require(!type.IsPlaying && State(talk) != 2 && text.text == assets.Localize("npc_talk_1"), "reopen displays new talk without stale end");
            CheckNpcSizes(root);
            Debug.Log("PARTNER_DETAIL_TALK_PASSED: exact native talk subtree, original typewriter/legacy clips, replace/disable/reopen and NPC size lookup. API-driven region verification; full detail touch-button interaction remains pending.");
        }
        finally
        {
            Object.Destroy(root);
            WorldMultiLanguageText.Localize = oldText;
            WorldMultiLanguageText.LanguageFont = oldFont;
            camera.clearFlags = oldClear;
            foreach (var child in active) if (child) child.gameObject.SetActive(true);
        }
    }

    static void CheckNpcSizes(GameObject root)
    {
        var go = new GameObject("Original NPC size controller fixture", typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        var controller = go.AddComponent<NpcCustomSizeController>();
        var fallback = new NpcCustomSizeController.NpcSizeConfig { Offset = new Vector2(12, -4), Scale = new Vector3(2, 3, 4) };
        var special = new NpcCustomSizeController.NpcSizeConfig { Offset = new Vector2(-8, 19), Scale = new Vector3(.8f, .8f, 1) };
        Set(controller, "mDefaultSizeConfig", fallback);
        Set(controller, "mSizeTarget", (RectTransform)go.transform);
        Require(!controller.TryGetConfig(8401, out var value) && value.Offset == fallback.Offset, "null dictionary uses original fallback");
        var dict = new MonoDictionary<int, NpcCustomSizeController.NpcSizeConfig>(); dict.Add(8401, special);
        Set(controller, "mNpcConfigDict", dict);
        controller.ApplyForNpc(8401);
        Require(((RectTransform)go.transform).anchoredPosition == special.Offset && go.transform.localScale == special.Scale, "configured original NPC transform");
        controller.ApplyForNpc(8402);
        Require(((RectTransform)go.transform).anchoredPosition == fallback.Offset && go.transform.localScale == fallback.Scale, "missing NPC restores original fallback");
        Set(controller, "mSizeTarget", null); controller.ApplyForNpc(8401);
        Object.Destroy(go);
    }
    static int State(NpcTalkUI talk)
    {
        var value = Get(talk, "mNpcTalkStateSwitcher");
        return Convert.ToInt32(value.GetType().GetProperty("State").GetValue(value));
    }
    static object Get(object owner, string field) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    static void Require(bool value, string message) { if (!value) throw new Exception("Original detail talk: " + message); }
    static void Capture(Camera camera, string name)
    {
        Canvas.ForceUpdateCanvases();
        var target = camera.targetTexture;
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var frame = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); frame.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), frame.EncodeToPNG());
        RenderTexture.active = null; Object.Destroy(frame);
    }
}

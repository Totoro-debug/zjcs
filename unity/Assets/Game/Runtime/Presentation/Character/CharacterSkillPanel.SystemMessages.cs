using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private GameObject systemMessage;
        private Coroutine messageRoutine;
        private Dictionary<string, RectTransform> messageNodes;
        private Dictionary<string, object> messageTemplate;

        private void ShowOriginalSystemMessage(string key)
        {
            var texts = CharacterModelPackage.AsObject(skillUiTemplates["gift_texts"], "original skill texts");
            string value = Convert.ToString(texts[key], CultureInfo.InvariantCulture);
            ShowOriginalSystemMessageValue(value);
        }

        private void ShowOriginalSystemMessageValue(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new InvalidOperationException("Original system message text is absent.");
            if (systemMessage == null)
            {
                messageTemplate = CharacterModelPackage.AsObject(skillUiTemplates["system_message"], "SystemMsgPanel");
                var layer = new GameObject("OfficialSkillSystemMessage", typeof(RectTransform), typeof(Canvas));
                Transform canvasRoot = GetComponentInParent<Canvas>().rootCanvas.transform;
                layer.transform.SetParent(canvasRoot, false);
                var rect = (RectTransform)layer.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                Canvas canvas = layer.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = Convert.ToInt32(messageTemplate["canvas_sorting_order"], CultureInfo.InvariantCulture);
                messageNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                factory.Build(CharacterPanelNodeFactory.ReadRoot(messageTemplate), rect, messageNodes,
                    new Dictionary<string, Button>(StringComparer.Ordinal));
                CharacterSafeArea.Restore(messageNodes["/SystemMsgPanel"], "/SystemMsgPanel", skillUiTemplates);
                foreach (Graphic graphic in layer.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                systemMessage = layer;
            }
            var fields = CharacterModelPackage.AsObject(messageTemplate["bindings"], "SystemMsgPanel fields");
            string textPath = Convert.ToString(fields["MsgText"], CultureInfo.InvariantCulture);
            SetAuxiliaryText(messageNodes[textPath], value);
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(ShowOriginalMessage(
                Convert.ToSingle(fields["ShowStayTime"], CultureInfo.InvariantCulture)));
        }

        private IEnumerator ShowOriginalMessage(float stayTime)
        {
            Dictionary<string, object> animationNode = FindMessageAnimationNode(
                CharacterModelPackage.AsObject(messageTemplate["root"], "SystemMsgPanel root"));
            var animations = (List<object>)animationNode["animations"];
            Dictionary<string, object> Clip(string name)
            {
                foreach (object raw in animations)
                {
                    var clip = CharacterModelPackage.AsObject(raw, "SystemMsgPanel clip");
                    if (CharacterPanelNodeFactory.String(clip, "name") == name) return clip;
                }
                throw new InvalidOperationException("Original system message animation is missing: " + name);
            }
            if (!TryBuildAnimationTracks(Clip("SystemMsgPanel_Show"), messageNodes, out var show, out float duration))
                throw new InvalidOperationException("The original system message show animation is unavailable.");
            yield return PlaySkillAnimation(show, duration, false);
            yield return new WaitForSeconds(Mathf.Max(0, stayTime - duration));
            if (!TryBuildAnimationTracks(Clip("SystemMsgPanel_Hide"), messageNodes, out var hide, out duration))
                throw new InvalidOperationException("The original system message hide animation is unavailable.");
            yield return PlaySkillAnimation(hide, duration, false);
            messageRoutine = null;
        }

        private static Dictionary<string, object> FindMessageAnimationNode(Dictionary<string, object> node)
        {
            if (node.ContainsKey("animations")) return node;
            foreach (object child in (List<object>)node["children"])
            {
                var result = FindMessageAnimationNode(CharacterModelPackage.AsObject(child, "message node"));
                if (result != null) return result;
            }
            return null;
        }
    }
}
